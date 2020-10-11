#region Namespaces
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Diagnostics;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI.Selection;
#endregion

namespace ViewDelCheck
{
    class App : IExternalApplication
    {
        /// <summary>
        /// Implements the OnStartup event
        /// </summary>
        /// <param name="application"></param>
        /// <returns></returns>
        /// 
      
        private AddInCommandBinding commandBinding_PRJBR_DEL;
        private AddInCommandBinding commandBinding_DEL;

        private ViewOwnerUpdater ownerParamUpdater;

        private ExternalEventExample after_hendler; //Внешнее событие которое запускается после закрытия окна 
        private ExternalEvent after_ex;             //редактирования шаблонов.

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                ownerParamUpdater = new ViewOwnerUpdater(application.ActiveAddInId);
                ownerParamUpdater.ParameterName = s_parameterName;

                UpdaterRegistry.RegisterUpdater(ownerParamUpdater);

                ElementClassFilter viewFilter = new ElementClassFilter(typeof(View));
                UpdaterRegistry.AddTrigger(ownerParamUpdater.GetUpdaterId(), viewFilter, Element.GetChangeTypeElementAddition());
            }
            catch (Exception ex)
            {
                ShowDialog("Ошибка", ex.Message);
            }

            after_hendler = new ExternalEventExample();
            after_ex = ExternalEvent.Create(after_hendler);

            // Lookup the desired command by name
            s_commandId_PRJBR_DEL = RevitCommandId.LookupCommandId(s_commandToDisable_PRJBR_DEL);
            s_commandId_DEL = RevitCommandId.LookupCommandId(s_commandToDisable_DEL);

            try
            {
                application.ControlledApplication.DocumentOpened
                    += new EventHandler<Autodesk.Revit.DB.Events.DocumentOpenedEventArgs>(DocOpenEvent);
                application.ControlledApplication.DocumentCreated
                    += new EventHandler<Autodesk.Revit.DB.Events.DocumentCreatedEventArgs>(DocCreateEvent);
                application.DialogBoxShowing
                    += new EventHandler<Autodesk.Revit.UI.Events.DialogBoxShowingEventArgs>(After);
            }
            catch(Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message);
                return Result.Failed;
            }

            // Confirm that the command can be overridden
            if (!s_commandId_PRJBR_DEL.CanHaveBinding)
            {
                ShowDialog("Error", "The target command " + s_commandToDisable_PRJBR_DEL +
                            " selected for disabling cannot be overridden");
                return Result.Failed;
            }
            if (!s_commandId_DEL.CanHaveBinding)
            {
                ShowDialog("Error", "The target command " + s_commandToDisable_DEL +
                            " selected for disabling cannot be overridden");
                return Result.Failed;
            }
            // Create a binding to override the command.
            // Note that you could also implement .CanExecute to override the accessibiliy of the command.
            // Doing so would allow the command to be grayed out permanently or selectively, however, 
            // no feedback would be available to the user about why the command is grayed out.
            try
            {
                commandBinding_PRJBR_DEL = application.CreateAddInCommandBinding(s_commandId_PRJBR_DEL);
                commandBinding_PRJBR_DEL.BeforeExecuted += NewEvent;
            }
            // Most likely, this is because someone else has bound this command already.
            catch (Exception)
            {
                ShowDialog("Error", "This add-in is unable to disable the target command " + s_commandToDisable_PRJBR_DEL +
                            "; most likely another add-in has overridden this command.");
            }

            try
            {
                commandBinding_DEL = application.CreateAddInCommandBinding(s_commandId_DEL);
                commandBinding_DEL.BeforeExecuted += NewEvent;
            }
            // Most likely, this is because someone else has bound this command already.
            catch (Exception)
            {
                ShowDialog("Error", "This add-in is unable to disable the target command " + s_commandToDisable_DEL +
                            "; most likely another add-in has overridden this command.");
            }

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            // Remove the command binding on shutdown
            if (s_commandId_PRJBR_DEL.HasBinding)
                application.RemoveAddInCommandBinding(s_commandId_PRJBR_DEL);

            if (s_commandId_DEL.HasBinding)
                application.RemoveAddInCommandBinding(s_commandId_DEL);

            application.ControlledApplication.DocumentOpened -= DocOpenEvent;
            application.ControlledApplication.DocumentCreated -= DocCreateEvent;
            application.DialogBoxShowing -= After;

            ownerParamUpdater = new ViewOwnerUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(ownerParamUpdater.GetUpdaterId());

            after_ex.Dispose();
            after_ex = null;
            after_hendler = null;

            return Result.Succeeded;
        }

        private void DocOpenEvent(object sender, Autodesk.Revit.DB.Events.DocumentOpenedEventArgs args)
        {
            Document doc = args.Document;

            if (!doc.IsFamilyDocument)
            {
                string username = doc.Application.Username;
                ownerParamUpdater.userName = username;

                if (!ParameterHelper.Check(doc, s_parameterName))
                {
                    ParameterHelper.CreateParam(doc, s_parameterName);
                }
            }
        }

        private void DocCreateEvent(object sender, Autodesk.Revit.DB.Events.DocumentCreatedEventArgs args)
        {
            Document doc = args.Document;

            if (!doc.IsFamilyDocument)
            {
                string username = doc.Application.Username;
                ownerParamUpdater.userName = username;

                if (!ParameterHelper.Check(doc, s_parameterName))
                {
                    ParameterHelper.CreateParam(doc, s_parameterName);
                }

                List<BuiltInCategory> cats = new List<BuiltInCategory>();
                cats.Add(BuiltInCategory.OST_Views);
                cats.Add(BuiltInCategory.OST_Sheets);

                ElementMulticategoryFilter mf = new ElementMulticategoryFilter(cats);

                IList<Element> viewsAndSheets = new FilteredElementCollector(doc)
                    .WherePasses(mf)
                    .ToElements();

                List<Element> elementList = new List<Element>();
                foreach (Element el in viewsAndSheets)
                {
                    if (el is ViewSchedule) continue;
                    if (el is View)
                    {
                        View view = el as View;
                        if (view.IsTemplate) continue;
                        elementList.Add(el);
                    }
                }

                if (elementList.Count != 0)
                {
                    ParameterHelper.SetParam(doc, elementList, username, s_parameterName);
                }
            }
        }

        private void NewEvent(object sender, BeforeExecutedEventArgs args)
        {
            UIApplication uiapp = sender as UIApplication;
            if (uiapp == null)
                return;

            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            Selection sel = uidoc.Selection;

            ICollection<ElementId> elemIds = sel.GetElementIds();

            foreach (ElementId id in elemIds)
            {
                if(doc.GetElement(id) is View)
                {  
                    if (doc.GetElement(id).LookupParameter(s_parameterName).AsString() == doc.Application.Username || !doc.GetElement(id).LookupParameter(s_parameterName).HasValue)
                    {
                        args.Cancel = false;
                    }
                    else
                    {
                        ShowDialog("Предупреждение!", "Вы пытаетесь удалить чужой вид/лист. \nОперация отменена.");
                        args.Cancel = true;
                    }   
                }  
            }
        }

        private void After(object sender, DialogBoxShowingEventArgs args)
        {
            if (args.DialogId == "Dialog_Revit_ViewTemplates")
            {
                after_ex.Raise();
            }
        }

        public static void ShowDialog(string title, string message)
        {
            // Show the user a message.
            TaskDialog td = new TaskDialog(title)
            {
                MainInstruction = message,
                TitleAutoPrefix = false
            };
            td.Show();
        }

        /// <summary>
        /// The string name of the command to disable.  To lookup a command id string, open a session of Revit, 
        /// invoke the desired command, close Revit, then look to the journal from that session.  The command
        /// id string will be toward the end of the journal, look for the "Jrn.Command" entry that was recorded
        /// when it was selected.
        /// </summary>
        static String s_commandToDisable_PRJBR_DEL = "ID_PRJBROWSER_DELETE";
        static String s_commandToDisable_DEL = "ID_BUTTON_DELETE";

        /// <summary>
        /// The command id, stored statically to allow for removal of the command binding.
        /// </summary>
        static RevitCommandId s_commandId_PRJBR_DEL;
        static RevitCommandId s_commandId_DEL;

        public static string s_parameterName = "Владелец";
    }
}
