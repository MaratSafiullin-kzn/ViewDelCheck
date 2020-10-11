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
    class ExternalEventExample : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            FilteredElementCollector views = new FilteredElementCollector(doc).OfClass(typeof(View));

            foreach(View view in views)
            {
                if (!view.IsTemplate) continue;
                ICollection<ElementId> nonControledParams = view.GetNonControlledTemplateParameterIds();
                nonControledParams.Add(view.LookupParameter(App.s_parameterName).Id);

                using (Transaction t = new Transaction(doc, "Шаблон"))
                {
                    t.Start();
                        view.SetNonControlledTemplateParameterIds(nonControledParams);
                    t.Commit();
                }  
            }
        }

        public string GetName()
        {
            return "AfterRevitDialogShowEvent ";
        }
    }
}
