using Microsoft.Win32;
using System.Activities;
using System.Windows;

namespace MaskDataDoc.Activities.Design.Designers
{
    /// <summary>
    /// Interaction logic for DocumentDataProtectionDesigner.xaml
    /// </summary>
    public partial class DocumentDataProtectionDesigner
    {
        public DocumentDataProtectionDesigner()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select input file",
                Filter = "Supported Files (*.docx;*.txt)|*.docx;*.txt|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                var inputArgument = ModelItem.Properties["InputFilePath"];
                if (inputArgument != null)
                {
                    inputArgument.SetValue(new InArgument<string>(dialog.FileName));
                }
            }
        }


    }
}
