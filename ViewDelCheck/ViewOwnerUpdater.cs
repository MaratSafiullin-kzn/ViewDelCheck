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
    public class ViewOwnerUpdater : IUpdater
    {
        static AddInId m_appId;
        static UpdaterId m_updaterId;

        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            this._userName = doc.Application.Username;
            ICollection<ElementId> ids = data.GetAddedElementIds();

            foreach (ElementId id in ids)
            {
                Element el = doc.GetElement(id);
                View view = el as View;

                if (el is ViewSchedule) continue;
                if (view.IsTemplate) continue;

                el.LookupParameter(_parameterName).Set(_userName);
            }
        }

        public ViewOwnerUpdater(AddInId id)
        {
            m_appId = id;
            m_updaterId = new UpdaterId(m_appId, new Guid("FBFBF6B2-4C06-42d4-97C1-D1B4EB593EFF"));
        }

        public string GetAdditionalInformation()
        {
            return "Назначает владельцем вида текущего пользователя";
        }

        public ChangePriority GetChangePriority()
        {
            return ChangePriority.Views;
        }

        public UpdaterId GetUpdaterId()
        {
            return m_updaterId;
        }

        public string GetUpdaterName()
        {
            return "View owner";
        }

        public string ParameterName
        {
            set
            {
                _parameterName = value;
            }
        }

        public string userName
        {
            set
            {
                _userName = value;
            }
        }

        private string _parameterName;
        private string _userName;
    }
}
