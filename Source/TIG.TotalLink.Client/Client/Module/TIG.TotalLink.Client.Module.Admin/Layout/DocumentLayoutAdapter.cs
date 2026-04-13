using DevExpress.Xpf.Docking;

namespace TIG.TotalLink.Client.Module.Admin.Layout
{
    public class DocumentLayoutAdapter : ILayoutAdapter
    {
        public string Resolve(DockLayoutManager owner, object item)
        {
            return owner.LayoutRoot.Name;
        }
    }
}
