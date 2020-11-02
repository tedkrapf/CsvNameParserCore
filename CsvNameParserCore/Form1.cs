using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BinaryFog.NameParser;
using CsvHelper;

namespace CsvNameParserCore
{
    public partial class Form1 : Form
    {
        private int TotalRecordsInCsvFile = 0;
        private BackgroundWorker bgWorker_BinaryFog;

        private class BackgroundParameters
        {
            public string FilePath { get; set; }
            public string ColumnNameToParse { get; set; }
        }

        public Form1()
        {
            InitializeComponent();

            openFileDialog1.Multiselect = false;
            openFileDialog1.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            bgWorker_BinaryFog = new BackgroundWorker();
            bgWorker_BinaryFog.WorkerReportsProgress = true;
            bgWorker_BinaryFog.DoWork += new System.ComponentModel.DoWorkEventHandler(bgWorker_BinaryFog_DoWork);
            bgWorker_BinaryFog.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(bgWorker_BinaryFog_ProgressChanged);
            bgWorker_BinaryFog.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(bgWorker_BinaryFog_RunWorkerCompleted);
            
        }

        private void btnSelectFile_Click(object sender, EventArgs e)
        {
            try
            {
                //openFileDialog1.InitialDirectory = "c:\\";
                openFileDialog1.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                openFileDialog1.FilterIndex = 1;
                openFileDialog1.RestoreDirectory = true;

                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    lblSelectedFilename.Text = openFileDialog1.FileName;

                    using (var reader = new StreamReader(openFileDialog1.FileName))
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        var records = csv.GetRecords<dynamic>();
                        var headers = records.First();
                        List<string> hs = new List<string>();
                        foreach (var h in headers)
                            hs.Add(h.Key.ToString());
                        ddlColumnToParse.DataSource = hs;
                        TotalRecordsInCsvFile = records.Count();
                    }
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = "ERROR: " + ex.Message + Environment.NewLine + ex.ToString()
                    + Environment.NewLine
                    + "-----------------------------------"
                    + Environment.NewLine
                    + txtStatus.Text;
            }
        }

        private void btnParse_Click(object sender, EventArgs e)
        {
            btnParse.Enabled = false;

            btnParse.Text = "processing";
            btnParse.Text = "... processing, please wait ...";

            //progBar.Value = 0;

            BackgroundParameters bp = new BackgroundParameters();
            bp.FilePath = lblSelectedFilename.Text;
            bp.ColumnNameToParse = ddlColumnToParse.SelectedValue.ToString();
            txtStatus.Text += Environment.NewLine + "... working on column: " + bp.ColumnNameToParse
                + Environment.NewLine + "... working on file: " + bp.FilePath
                + Environment.NewLine + "... number of rows to parse: " + TotalRecordsInCsvFile.ToString();

            bgWorker_BinaryFog.RunWorkerAsync(bp);
        }

        private void bgWorker_BinaryFog_DoWork(object sender, DoWorkEventArgs e)
        {
            var bgParams = (BackgroundParameters)e.Argument;

            var outputRows = new List<dynamic>();
            int cntRows = 0;
            int cntParsed = 0;
            int cntErrored = 0;
            string errs = Environment.NewLine + "ERRORS: ";
            string msg = "";
            string haltMsg = "";

            try
            {
                using (var reader = new StreamReader(bgParams.FilePath))
                {
                    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        var csvRows = csv.GetRecords<dynamic>();
                        foreach (var csvRow in csvRows)
                        {
                            cntRows++;

                            int percentage = (cntRows) * 100 / (TotalRecordsInCsvFile - 1);

                            bgWorker_BinaryFog.ReportProgress(percentage);

                            dynamic newRow = new ExpandoObject();
                            newRow = DeepCopy(csvRow);

                            foreach (var column in csvRow)
                            {
                                try
                                {
                                    if (column.Key.ToString() == bgParams.ColumnNameToParse.ToString())
                                    {

                                        BinaryFog.NameParser.FullNameParser target = new BinaryFog.NameParser.FullNameParser(column.Value.ToString());
                                        target.Parse();
                                        newRow._ParsedNameValueToTheRight = " ~~~> ";
                                        newRow._CompanyName = "";
                                        newRow._FirstName = target.FirstName;
                                        newRow._MiddleName = target.MiddleName;
                                        newRow._LastName = target.LastName;
                                        newRow._Title = target.Title;
                                        newRow._NickName = target.NickName;
                                        newRow._Suffix = target.Suffix;
                                        newRow._DisplayName = target.DisplayName;
                                        newRow._ParseScore = target.Results.First().Score;

                                        cntParsed++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    cntErrored++;
                                    newRow._ParsedNameValueToTheRight = " ~~~> ";
                                    newRow._CompanyName = column.Value.ToString();
                                    newRow._FirstName = "";
                                    newRow._MiddleName = "";
                                    newRow._LastName = "";
                                    newRow._Title = "";
                                    newRow._NickName = "";
                                    newRow._Suffix = "";
                                    newRow._DisplayName = column.Value.ToString();
                                    newRow._ParseScore = -1;
                                    errs += Environment.NewLine + "on row: " + cntRows.ToString() + ": " + ex.Message;
                                }
                            }
                            outputRows.Add(newRow);
                        }
                    }
                }

            HaltAndCatchFire:

                string outputFilename = bgParams.FilePath.Replace(".csv", "_PARSED_AT_" + DateTime.Now.ToString("yyyyMMdd_hhmmss") + ".csv");

                using (var writer = new StreamWriter(outputFilename))
                {
                    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        csv.WriteRecords(outputRows);
                    }
                }

                msg += Environment.NewLine
                    + "FINISHED!!" + Environment.NewLine
                    + "  File saved to: " + outputFilename + Environment.NewLine
                    + "  Total Rows:  " + cntRows + Environment.NewLine
                    + "  Rows Parsed: " + cntParsed + Environment.NewLine
                    + "  Rows Errors: " + cntErrored + Environment.NewLine
                    + "  " + (cntErrored > 0 ? " (see error details below)" + Environment.NewLine + errs + Environment.NewLine : "")
                    + Environment.NewLine + txtStatus.Text;
            }
            catch (Exception ex)
            {
                msg = "ERROR: " + ex.Message + Environment.NewLine + ex.ToString()
                    + Environment.NewLine
                    + "-----------------------------------"
                    + Environment.NewLine
                    + txtStatus.Text;
            }

            e.Result = msg;
        }

        private void bgWorker_BinaryFog_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage <= 100)
                progBar.Value = e.ProgressPercentage;
        }

        private void bgWorker_BinaryFog_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnParse.Enabled = true;
            btnParse.Text = "Parse (Locally)";
            MessageBox.Show("Done!");
            progBar.Value = 0;
            txtStatus.Text = e.Result.ToString();
        }


        #region helpers

        static ExpandoObject ShallowCopy(ExpandoObject original)
        {
            var clone = new ExpandoObject();

            var _original = (IDictionary<string, object>)original;
            var _clone = (IDictionary<string, object>)clone;

            foreach (var kvp in _original)
                _clone.Add(kvp);

            return clone;
        }

        static ExpandoObject DeepCopy(ExpandoObject original)
        {
            var clone = new ExpandoObject();

            var _original = (IDictionary<string, object>)original;
            var _clone = (IDictionary<string, object>)clone;

            foreach (var kvp in _original)
                _clone.Add(kvp.Key, kvp.Value is ExpandoObject ? DeepCopy((ExpandoObject)kvp.Value) : kvp.Value);

            return clone;
        }

        #endregion
    }
}
