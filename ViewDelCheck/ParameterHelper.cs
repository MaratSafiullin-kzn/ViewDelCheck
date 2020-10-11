#region Namespaces
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI.Selection;
#endregion

namespace ViewDelCheck
{
    public static class ParameterHelper
    {
        public static bool Check(Document doc, string name)
        {
            BindingMap map = doc.ParameterBindings;
            DefinitionBindingMapIterator it = map.ForwardIterator();
            Category viewCategory = Category.GetCategory(doc, BuiltInCategory.OST_Views);

            it.Reset();
            while (it.MoveNext())
            {
                ElementBinding binding = it.Current as ElementBinding;
                CategorySet cats = binding.Categories;

                if (it.Key.Name == name && cats.Contains(viewCategory))
                {
                    return true;
                }
            }
            return false;
        }

        public static void SetParam(Document doc, List<Element> elementList, string username, string parametername)
        {
            try
            {
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Запись параметра");
                    foreach (Element el in elementList)
                    {
                        el.LookupParameter(parametername).Set(username);
                    }
                    t.Commit();
                }
            }
            catch(Exception ex)
            {
                App.ShowDialog("Ошибка записи параметра", ex.Message);
            }
        }

        public static void CreateParam(Document doc, string s_parameterName)
        {
            try
            {
                Application app = doc.Application;

                String modulePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                String paramFile = modulePath + "\\ViewDelCheckSharedParameters.txt";

                if (File.Exists(paramFile))
                {
                    File.Delete(paramFile);
                }

                FileStream fs = File.Create(paramFile);
                fs.Close();

                app.SharedParametersFilename = paramFile;

                DefinitionFile parafile = app.OpenSharedParameterFile();
                DefinitionGroup apiGroup = parafile.Groups.Create("Views");

                ExternalDefinitionCreationOptions ExternalDefinitionCreationOptions = new ExternalDefinitionCreationOptions(s_parameterName, ParameterType.Text);
                ExternalDefinitionCreationOptions.UserModifiable = false;

                Definition s_parameterNameDef = apiGroup.Definitions.Create(ExternalDefinitionCreationOptions);

                Category viewCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Views);
                Category sheetCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Sheets);
                CategorySet categories = app.Create.NewCategorySet();
                categories.Insert(sheetCat);
                categories.Insert(viewCat);

                InstanceBinding binding = app.Create.NewInstanceBinding(categories);

                using (Autodesk.Revit.DB.Transaction t = new Autodesk.Revit.DB.Transaction(doc))
                {
                    t.Start("Создание параметра");
                    doc.ParameterBindings.Insert(s_parameterNameDef, binding);
                    t.Commit();
                }
            }
            catch(Exception ex)
            {
                App.ShowDialog("Ошибка создания параметра", ex.Message);
            }
        }
    }
}
