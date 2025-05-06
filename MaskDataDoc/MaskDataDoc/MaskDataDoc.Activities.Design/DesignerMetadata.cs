using System.Activities.Presentation.Metadata;
using System.ComponentModel;
using System.ComponentModel.Design;
using MaskDataDoc.Activities.Design.Designers;
using MaskDataDoc.Activities.Design.Properties;

namespace MaskDataDoc.Activities.Design
{
    public class DesignerMetadata : IRegisterMetadata
    {
        public void Register()
        {
            var builder = new AttributeTableBuilder();
            builder.ValidateTable();

            var categoryAttribute = new CategoryAttribute($"{Resources.Category}");

            builder.AddCustomAttributes(typeof(DocumentDataProtection), categoryAttribute);
            builder.AddCustomAttributes(typeof(DocumentDataProtection), new DesignerAttribute(typeof(DocumentDataProtectionDesigner)));
            builder.AddCustomAttributes(typeof(DocumentDataProtection), new HelpKeywordAttribute(""));


            MetadataStore.AddAttributeTable(builder.CreateTable());
        }
    }
}
