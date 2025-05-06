using System;
using System.Activities;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MaskDataDoc.Activities.Properties;
using UiPath.Shared.Activities;
using UiPath.Shared.Activities.Localization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Linq;

namespace MaskDataDoc.Activities
{
    [LocalizedDisplayName(nameof(Resources.DocumentDataProtection_DisplayName))]
    [LocalizedDescription(nameof(Resources.DocumentDataProtection_Description))]
    public class DocumentDataProtection : ContinuableAsyncCodeActivity
    {
        #region Properties

        [LocalizedCategory(nameof(Resources.Common_Category))]
        [LocalizedDisplayName(nameof(Resources.ContinueOnError_DisplayName))]
        [LocalizedDescription(nameof(Resources.ContinueOnError_Description))]
        public override InArgument<bool> ContinueOnError { get; set; }

        [LocalizedCategory(nameof(Resources.Common_Category))]
        [LocalizedDisplayName(nameof(Resources.Timeout_DisplayName))]
        [LocalizedDescription(nameof(Resources.Timeout_Description))]
        public InArgument<int> TimeoutMS { get; set; } = 60000;

        [LocalizedDisplayName(nameof(Resources.DocumentDataProtection_InputFilePath_DisplayName))]
        [LocalizedDescription(nameof(Resources.DocumentDataProtection_InputFilePath_Description))]
        [LocalizedCategory(nameof(Resources.Input_Category))]
        public InArgument<string> InputFilePath { get; set; }

        public bool MaskEmail { get; set; }
        public bool MaskPhone { get; set; }
        public bool MaskPassword { get; set; }
        public bool MaskIBAN { get; set; }
        public bool MaskCreditCard { get; set; }
        public bool MaskLicensePlate { get; set; }
        public bool MaskCNP { get; set; }
        public bool MaskName { get; set; }  // Added for Name masking
        public bool MaskAddress { get; set; }  // Added for Address masking
        public bool MaskDateOfBirth { get; set; }  // Added for Date of Birth masking

        [LocalizedDisplayName(nameof(Resources.DocumentDataProtection_OutputFilePath_DisplayName))]
        [LocalizedDescription(nameof(Resources.DocumentDataProtection_OutputFilePath_Description))]
        [LocalizedCategory(nameof(Resources.Output_Category))]
        public OutArgument<string> OutputFilePath { get; set; }

        #endregion

        protected override async Task<Action<AsyncCodeActivityContext>> ExecuteAsync(AsyncCodeActivityContext context, CancellationToken cancellationToken)
        {
            var timeout = TimeoutMS.Get(context);
            var inputFilePath = InputFilePath.Get(context);

            var task = ExecuteWithTimeout(context, cancellationToken, inputFilePath);
            if (await Task.WhenAny(task, Task.Delay(timeout, cancellationToken)) != task)
                throw new TimeoutException("The operation timed out.");

            return (ctx) =>
            {
                var extension = Path.GetExtension(inputFilePath);
                var output = Path.Combine(Path.GetDirectoryName(inputFilePath),
                    Path.GetFileNameWithoutExtension(inputFilePath) + ".masked" + extension);
                OutputFilePath.Set(ctx, output);
            };
        }

        private async Task ExecuteWithTimeout(AsyncCodeActivityContext context, CancellationToken cancellationToken, string inputFilePath)
        {
            string extension = Path.GetExtension(inputFilePath).ToLower();
            string outputPath = Path.Combine(Path.GetDirectoryName(inputFilePath),
                Path.GetFileNameWithoutExtension(inputFilePath) + ".masked" + extension);

            switch (extension)
            {
                case ".txt":
                    string content = await File.ReadAllTextAsync(inputFilePath, cancellationToken);
                    string maskedContent = ApplyMasking(content);
                    await File.WriteAllTextAsync(outputPath, maskedContent, cancellationToken);
                    break;

                case ".docx":
                    using (MemoryStream mem = new MemoryStream())
                    {
                        // Copiem fișierul sursă în memorie pentru a putea lucra pe o copie
                        using (FileStream fileStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read))
                        {
                            fileStream.CopyTo(mem);
                        }

                        using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(mem, true))
                        {
                            var body = wordDoc.MainDocumentPart.Document.Body;
                            foreach (var para in body.Elements<Paragraph>())
                            {
                                foreach (var run in para.Elements<Run>())
                                {
                                    foreach (var text in run.Elements<Text>())
                                    {
                                        text.Text = ApplyMasking(text.Text);
                                    }
                                }
                            }

                            wordDoc.MainDocumentPart.Document.Save();
                        }

                        // Scriem rezultatul modificat în fișierul de output
                        await File.WriteAllBytesAsync(outputPath, mem.ToArray(), cancellationToken);
                    }
                    break;
            }
        }

        private string ApplyMasking(string content)
        {
            // Definim un pattern pentru numerele de dosar, cum ar fi '2-3922/21'
            string dosarPattern = @"\b\d{1,2}-\d{1,4}\/\d{2}\b";

            // Nu aplica mascare pe numerele de dosar
            content = Regex.Replace(content, dosarPattern, match => match.Value);

            // Aplica mascare pentru CNP
            if (MaskCNP)
            {
                content = Regex.Replace(content, @"\b\d{13}\b", "*******#######");
            }

            // Aplica mascare pentru Email
            if (MaskEmail)
            {
                content = Regex.Replace(content, @"([a-zA-Z0-9._%+-]+)@([a-zA-Z0-9.-]+\.[a-zA-Z]{2,})", "***@***.***");
            }

            // Aplica mascare pentru numere de telefon
            if (MaskPhone)
            {
                content = Regex.Replace(content, @"(\+40\s?)?(\(?\d{2,3}\)?[\s\-]?)?\d{3}[\s\-]?\d{3}[\s\-]?\d{3}", "+40 *** *** ***");
            }

            // Aplica mascare pentru parole
            if (MaskPassword)
            {
                content = Regex.Replace(content, @"(?i)(parol[ăa]?|password|pass)[\s:=\-]*\S+", "$1: ********");
            }

            // Aplica mascare pentru IBAN
            if (MaskIBAN)
            {
                content = Regex.Replace(content, @"\bRO\d{2}[A-Z]{4}\d{16}\b", "RO** **** **** **** **** ****");
            }

            // Aplica mascare pentru carduri de credit
            if (MaskCreditCard)
            {
                content = Regex.Replace(content, @"\b(?:\d{4}[- ]?){3}\d{4}\b", "****-****-****-####");
            }

            // Aplica mascare pentru plăcuțe de înmatriculare
            if (MaskLicensePlate)
            {
                content = Regex.Replace(content, @"\b([A-Z]{1,2}\d{2,3}[A-Z]{1,3})\b", "***###");
            }


            // Aplica mascare pentru nume simplificate
            if (MaskName)
            {

                //string keywords = @"(?i)\b(?:împotriva|de la|de|familia|domnul|doamna|dna|dl|persoana|minorul|copilul|clientul|petentul|intimatul|reclamantul|pârâtul|beneficiarul|mandatarul|titularul|nume|prenume|cumpărător|vânzător|prestator|beneficiar|client|furnizor|reprezentant|instituție|societate|companie|persoană fizică|titular|director|administrator|angajat|utilizator|locator|mandatar|avocat|entitate|agenție|autoritate|întreprindere|firma|firmă|organization|company|buyer|seller|provider|contractor|representative|employee|manager|lawyer|agent|user|holder|customer)\b";
                string keywords = @"(?i)\b(?:copilului)\b";

                string namePattern = @"\s[:-–]?\s([A-ZĂÂÎȘȚ][\p{L}’'-]+(?:\s+[A-ZĂÂÎȘȚ][\p{L}’'-]+){0,2})";

                string pattern = keywords + namePattern;
                content = Regex.Replace(content, pattern, match => match.Value.Replace(match.Groups[1].Value, "***"));

            }



            // Aplica mascare pentru adresă (simplificată)
            if (MaskAddress)
            {
                content = Regex.Replace(content, @"(\d{1,5}\s[A-Za-z]+\s[A-Za-z]+)", "*****");
            }

            // Aplica mascare pentru data nașterii
            if (MaskDateOfBirth)
            {
                content = Regex.Replace(content, @"\b\d{1,2}[-./]?\d{1,2}[-./]?\d{4}\b", "**.**.****");
            }

            return content;
        }



    }
}

