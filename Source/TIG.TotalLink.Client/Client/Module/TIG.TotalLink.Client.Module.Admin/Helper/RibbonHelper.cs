using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Xml;
using AutoMapper;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Ribbon.Core;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;

namespace TIG.TotalLink.Client.Module.Admin.Helper
{
    public class RibbonHelper
    {
        /// <summary>
        /// Generates a ribbon structure from an Xml file stored as a Resource.
        /// </summary>
        /// <param name="uriResource">The Uri that maps to an embedded resource.</param>
        /// <param name="categoryCollection">A collection of Ribbon Categories to store the ribbon structure in.</param>
        public static void LoadRibbonFromXml(Uri uriResource, IList<RibbonCategoryViewModelBase> categoryCollection)
        {
            // Get a stream that refers to the resource
            var resource = Application.GetResourceStream(uriResource);
            if (resource == null)
                return;

            // Load the resource into an XmlDocument
            var doc = new XmlDocument();
            using (var stream = resource.Stream)
            {
                doc.Load(stream);
            }

            // Get the root element of the XmlDocument
            var rootElement = doc.DocumentElement;
            if (rootElement == null)
                return;

            // Recursively load all elements
            var categories = ProcessChildElementsRecursive(rootElement, null);

            // Map the created RibbonCategories to RibbonCategoryViewModels
            Mapper.Map(categories, categoryCollection);
        }

        /// <summary>
        /// Generates ribbon components from each child element of the supplied element.
        /// </summary>
        /// <param name="element">The element to process children in.</param>
        /// <param name="parent">The parent component to assign to created components.</param>
        /// <returns>A list of RibbonCategory.</returns>
        private static List<RibbonCategory> ProcessChildElementsRecursive(XmlElement element, DataObjectBase parent)
        {
            var categories = new List<RibbonCategory>();

            foreach (var childElement in element.ChildNodes.OfType<XmlElement>())
            {
                switch (childElement.Name)
                {
                    case "RibbonCategory":
                        var category = new RibbonCategory()
                        {
                            IsDefault = childElement.GetBoolAttribute("IsDefault")
                        };
                        categories.Add(category);
                        ProcessChildElementsRecursive(childElement, category);
                        break;

                    case "RibbonPage":
                        var page = new RibbonPage()
                        {
                            Name = childElement.GetAttribute("Name"),
                            RibbonCategory = parent as RibbonCategory
                        };
                        ProcessChildElementsRecursive(childElement, page);
                        break;

                    case "RibbonGroup":
                        var group = new RibbonGroup()
                        {
                            Name = childElement.GetAttribute("Name"),
                            RibbonPage = parent as RibbonPage
                        };
                        ProcessChildElementsRecursive(childElement, group);
                        break;

                    case "RibbonItem":
                        var item = new RibbonItem()
                        {
                            Name = childElement.GetAttribute("Name"),
                            ItemType = childElement.GetEnumAttribute<RibbonItemType>("ItemType"),
                            CommandType = childElement.GetEnumAttribute<CommandType>("CommandType"),
                            CommandParameter = childElement.GetAttribute("CommandParameter"),
                            RibbonGroup = parent as RibbonGroup
                        };
                        break;
                }
            }

            return categories;
        }
    }
}
