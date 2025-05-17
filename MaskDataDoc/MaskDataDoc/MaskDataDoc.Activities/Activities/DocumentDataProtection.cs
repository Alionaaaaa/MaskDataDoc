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
using System.Collections.Generic;
using System.Text;

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
                        // Citim fișierul original în memorie
                        using (FileStream fileStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read))
                        {
                            fileStream.CopyTo(mem);
                        }

                        mem.Position = 0;
                        using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(mem, true))
                        {
                            var body = wordDoc.MainDocumentPart.Document.Body;

                            // Procesăm fiecare paragraf separat
                            foreach (var paragraph in body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                            {
                                var textElements = paragraph.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().ToList();

                                // Concatenăm tot textul din paragraf
                                var paragraphText = string.Concat(textElements.Select(te => te.Text));

                                // Aplicăm mascarea textului pe paragraf
                                string maskedParagraphText = ApplyMasking(paragraphText);

                                // Reconstruim textul înapoi în elementele <w:t>
                                int currentIndex = 0;
                                foreach (var textElem in textElements)
                                {
                                    int length = textElem.Text.Length;
                                    if (currentIndex + length <= maskedParagraphText.Length)
                                    {
                                        textElem.Text = maskedParagraphText.Substring(currentIndex, length);
                                        currentIndex += length;
                                    }
                                    else if (currentIndex < maskedParagraphText.Length)
                                    {
                                        textElem.Text = maskedParagraphText.Substring(currentIndex);
                                        currentIndex = maskedParagraphText.Length;
                                    }
                                    else
                                    {
                                        // Dacă am terminat textul mascat, restul elementelor devin goale
                                        textElem.Text = "";
                                    }
                                }
                            }

                            wordDoc.MainDocumentPart.Document.Save();
                        }

                        // Salvăm fișierul modificat în outputPath
                        await File.WriteAllBytesAsync(outputPath, mem.ToArray(), cancellationToken);
                    }
                    break;

            }
        }



        private string ApplyMasking(string content)
        {
            //// Definim un pattern pentru numerele de dosar, cum ar fi '2-3922/21'
            //string dosarPattern = @"\b\d{1,2}-\d{1,4}\/\d{2}\b";

            //// Nu aplica mascare pe numerele de dosar
            //content = Regex.Replace(content, dosarPattern, match => match.Value);

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




            if (MaskName)
            {
                var sensitiveRoles = new List<string>
    {
         "reclamant", "reclamantul", "reclamantei", "pârât", "pârâtul", "pârâtei",
            "intervenient", "intervenientul", "petent", "petentul", "contestator", "contestatorul",
            "apelant", "apelantul", "intimat", "intimatul", "inculpat", "inculpatul", "învinuit",
            "suspect", "condamnat", "persoana vătămată", "minor", "copil","copilului","copiilor","familia", "părintele", "soț", "soția",
            "moștenitor", "debitor", "creditor", "titular", "beneficiar", "pacient", "angajat",
            "salariat", "proprietar", "cetățean", "persoană fizică", "fiul", "fiica", "rudă", "împotriva"
    };

                foreach (var role in sensitiveRoles)
                {
                    string pattern = $@"\b{role}\s+([A-ZĂÂÎȘȚ][a-zăâîșțéëäöü]+)\s+([A-ZĂÂÎȘȚ][a-zăâîșțéëäöü]+)";
                    content = Regex.Replace(content, pattern, m =>
                    {
                        string prenume = m.Groups[1].Value;
                        string initiala = !string.IsNullOrEmpty(prenume) ? prenume[0] + "." : "";
                        return $"{role} {initiala} *****";
                    }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }
            }





            if (MaskAddress)
            {
                var sensitiveAddressTerms = new List<string>
    {
                    // Formulări generale
    "cu domiciliul în",
    "domiciliul",
    "locuiește în",
    "adresa",
    "cu reședința în",
    "reședința",
    "residence at",
    "locația",
    "domiciliu",
    "reședință",

    // Termeni specifici de adresă
    "strada",
    "str.",
    "bd.",
    "bulevardul",
    "aleea",
    "nr.",
    "numărul",
    "bloc",
    "apartament",
    "etaj",
    "scara",
    "cartier",
    "localitate",
    "oraș",
    "municipiu",
    "sat"
    };

                foreach (var term in sensitiveAddressTerms)
                {
                    // Pattern care găsește termnul urmat de unul sau mai multe cuvinte ce reprezintă adresa
                    // Exemplu: "strada Mihail Kogălniceanu 10", "bd. Republicii 5A"
                    string pattern = $@"\b{term}\s+([\wăâîșțĂÂÎȘȚ\d\-\/]+(\s+[\wăâîșțĂÂÎȘȚ\d\-\/]+){{0,4}})";

                    content = Regex.Replace(content, pattern, m =>
                    {
                        // m.Groups[1] este partea care conține adresa, o mascăm complet cu ***
                        return $"{term} ***";
                    }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }
            }







            // Aplica mascare pentru data nașterii
            if (MaskDateOfBirth)
            {
                content = Regex.Replace(content, @"\b(0?[1-9]|[12][0-9]|3[01])[-./](0?[1-9]|1[012])[-./](19|20)\d\d\b", "**.**.****");

            }

            return content;
        }



    }
}

