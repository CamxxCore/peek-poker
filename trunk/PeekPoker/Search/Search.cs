using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using PeekPoker.Interface;

namespace PeekPoker.Search
{
    public partial class Search : Form
    {
        public event ShowMessageBoxHandler ShowMessageBox;
        public event UpdateProgressBarHandler UpdateProgressbar;
        public event EnableControlHandler EnableControl;
        public event GetTextBoxTextHandler GetTextBoxText;

        private BindingList<SearchResults> _searchResult = new BindingList<SearchResults>();
        private BindingList<SearchResults> _searchLimitedResult = new BindingList<SearchResults>();
        private readonly RealTimeMemory _rtm;
        private RwStream _readWriter;

        // temporary position and length for refreshing purpose
        private string _tempLength;
        private string _tempOffset;
        private string _id;
        private string _length;
        public Search(RealTimeMemory rtm)
        {
            this.InitializeComponent();
            this._rtm = rtm;
            this.resultGrid.DataSource = this._searchLimitedResult;
        }

        //Control changes
        private void GridRowColours(int value)
        {
            if (this.resultGrid.InvokeRequired)
                this.resultGrid.Invoke((MethodInvoker)(() => this.GridRowColours(value)));
            else
                this.resultGrid.Rows[value].DefaultCellStyle.ForeColor = Color.Red;
        }

        private void SearchRangeButtonClick(object sender, EventArgs e)
        {
            try
            {
                this._tempLength = this.GetTextBoxText(this.lengthRangeAddressTextBox);
                this._tempOffset = this.GetTextBoxText(this.startRangeAddressTextBox);
                Thread oThread = new Thread(this.SearchRange);
                oThread.Start();
            }
            catch (Exception ex)
            {
                this.ShowMessageBox(ex.Message, string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        //Refresh results Thread
        private void RefreshResultList()
        {
            try
            {
                this.EnableControl(this.resultRefreshButton, false);
                BindingList<SearchResults> newSearchResults = new BindingList<SearchResults>();
                BindingList<SearchResults> limitSearchResults = new BindingList<SearchResults>();
                var value = 0;
                string retvalue = "";

                if (this._searchResult.Count > 500)
                {
                    var results = this._rtm.Peek(this._tempOffset, this._tempLength, this._tempOffset, this._tempLength);

                    this._readWriter = new RwStream();
                    try
                    {
                        byte[] _buffer = Functions.HexToBytes(results);
                        this._readWriter.WriteBytes(_buffer, 0, _buffer.Length);

                        this._readWriter.Flush();
                        this._readWriter.Position = 0;
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                    
                }
                
                foreach (var item in this._searchResult)
                {
                    this.UpdateProgressbar(0, this._searchResult.Count, value, "Refreshing...");

                    var length = (item.Value.Length / 2).ToString("X");
                    if (this._searchResult.Count > 500)
                    {
                        //var retvalue = this._rtm.
                        uint pos = uint.Parse(item.Offset, System.Globalization.NumberStyles.AllowHexSpecifier);
                        uint spos = uint.Parse(this._tempOffset, System.Globalization.NumberStyles.AllowHexSpecifier);
                        this._readWriter.Position = (pos - spos);
                        byte[] _value = this._readWriter.ReadBytes((item.Value.Length / 2));
                        retvalue = Functions.ToHexString(_value);
                        
                    }
                    else
                    {
                        retvalue = this._rtm.Peek(item.Offset, length, item.Offset, length);
                    }

                    uint currentResults;
                    uint newResult;

                    if (!uint.TryParse(item.Value, out currentResults))
                        throw new Exception("Invalid Search Value this function only works for Unsigned Integers.");
                    uint.TryParse(retvalue, out newResult);

                    //===================================================
                    //Default
                    if(this.defaultRadioButton.Checked)
                    {
                        if (item.Value == retvalue) continue; //if value hasn't change continue for each loop
                        if (value < 1000)
                        {
                            this.GridRowColours(value);
                        }
                        item.Value = retvalue;
                    }
                    else if (this.ifEqualsRadioButton.Checked)
                    {
                        if (newResult == currentResults)
                        {
                            SearchResults searchResultItem = new SearchResults
                                                                 {
                                                                     ID = item.ID,
                                                                     Offset = item.Offset,
                                                                     Value = retvalue
                                                                 };
                            newSearchResults.Add(searchResultItem);
                        }
                    }
                    else if (this.ifGreaterThanRadioButton.Checked)
                    {
                        if (newResult > currentResults)
                        {
                            SearchResults searchResultItem = new SearchResults
                            {
                                ID = item.ID,
                                Offset = item.Offset,
                                Value = retvalue
                            };
                            newSearchResults.Add(searchResultItem);
                        }
                    }
                    else if (this.ifLessThanRadioButton.Checked)
                    {
                        if (newResult < currentResults)
                        {
                            SearchResults searchResultItem = new SearchResults
                            {
                                ID = item.ID,
                                Offset = item.Offset,
                                Value = retvalue
                            };
                            newSearchResults.Add(searchResultItem);
                        }
                    }
                    else if (this.ifLessThanRadioButton.Checked)
                    {
                        if (newResult != currentResults)
                        {
                            SearchResults searchResultItem = new SearchResults
                            {
                                ID = item.ID,
                                Offset = item.Offset,
                                Value = retvalue
                            };
                            newSearchResults.Add(searchResultItem);
                        }
                    }
                    else if (this.ifChangeRadioButton.Checked)
                    {
                        if (item.Value != retvalue)
                        {
                            SearchResults searchResultItem = new SearchResults
                            {
                                ID = item.ID,
                                Offset = item.Offset,
                                Value = retvalue
                            };
                            newSearchResults.Add(searchResultItem);
                        } 
                    }

                    value++;
                }
                if (this.defaultRadioButton.Checked)
                {
                    this.ResultGridUpdate();
                    this.ResultCountBoxUpdate();
                }
                else
                {
                    this._searchResult = newSearchResults;
                    for (int i = 0; i < this._searchResult.Count; i++)
                    {
                        if (i >= 1000)
                            break;

                        limitSearchResults.Add(this._searchResult[i]);
                    }

                    this._searchLimitedResult = limitSearchResults;
                    this.ResultGridUpdate();
                    this.ResultCountBoxUpdate();
                }
                this.UpdateProgressbar(0, 100, 0, "idle");
            }
            catch (Exception ex)
            {
                this.ShowMessageBox(ex.Message, string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.EnableControl(this.resultRefreshButton, true);
                this.UpdateProgressbar(0, 100, 0, "idle");
                Thread.CurrentThread.Abort();
            }
        }

        // Refresh results
        private void ResultRefreshClick(object sender, EventArgs e)
        {
            if (this._searchResult.Count > 0)
            {
                var thread = new Thread(this.RefreshResultList);
                thread.Start();
            }
            else
            {
                this.ShowMessageBox("Can not refresh! \r\n Result list empty!!", string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResultGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            var cell = (DataGridCell)sender;
            if (this.resultGrid.Rows[cell.RowNumber].Cells[2].Value != null)
                this.resultGrid.Rows[cell.RowNumber].DefaultCellStyle.ForeColor = Color.Red;
        }

        private void SearchRangeValueTextBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Return || !this.searchRangeValueTextBox.Focused) return;
            var oThread = new Thread(this.SearchRange);
            oThread.Start();
            e.Handled = true;
            this.searchRangeButton.Focus();
        }

        //Searches the memory for the specified value (Experimental)
        private void SearchRange()
        {
            try
            {
                this.EnableControl(this.searchRangeButton, false);
                this.EnableControl(this.stopSearchButton, true);
                this._rtm.DumpOffset = Functions.Convert(this.GetTextBoxText(this.startRangeAddressTextBox));
                this._rtm.DumpLength = Functions.Convert(this.GetTextBoxText(this.lengthRangeAddressTextBox));

                this.ResultGridClean();//Clean list view

                //The ExFindHexOffset function is a Experimental search function
                var results = this._rtm.FindHexOffset(this.GetTextBoxText(this.searchRangeValueTextBox));//pointer
                //Reset the progressbar...
                this.UpdateProgressbar(0, 100, 0);

                if (results.Count < 1)
                {
                    this.ShowMessageBox(string.Format("No result/s found!"), string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return; //We don't want it to continue
                }

                this._searchResult = results;
                BindingList<SearchResults> newLimitResult = new BindingList<SearchResults>();

                for (int i = 0; i < this._searchResult.Count; i++)
                {
                    if (i >= 1000)
                        break;

                    newLimitResult.Add(this._searchResult[i]);
                }
                this._searchLimitedResult = newLimitResult;
                this.ResultGridUpdate();
                this.ResultCountBoxUpdate();
            }
            catch (Exception e)
            {
                this.ShowMessageBox(e.Message, string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.EnableControl(this.searchRangeButton, true);
                this.EnableControl(this.stopSearchButton, false);
                Thread.CurrentThread.Abort();
            }
        }


        //Refresh the values of Search Results
        private void ResultGridClean()
        {
            if (this.resultGrid.InvokeRequired)
                this.resultGrid.Invoke((MethodInvoker)(this.ResultGridClean));
            else
                this.resultGrid.Rows.Clear();
        }
        private void ResultGridUpdate()
        {
            //IList or represents a collection of objects(String)
            if (this.resultGrid.InvokeRequired)
                //lambda expression empty delegate that calls a recursive function if InvokeRequired
                this.resultGrid.Invoke((MethodInvoker)(this.ResultGridUpdate));
            else
            {
                this.resultGrid.DataSource = this._searchLimitedResult;
                this.resultGrid.Refresh();
            }
        }
        //ResultCountBox
        private void ResultCountBoxUpdate()
        {
            //IList or represents a collection of objects(String)
            if (this.ResultCountBox.InvokeRequired)
                //lambda expression empty delegate that calls a recursive function if InvokeRequired
                this.ResultCountBox.Invoke((MethodInvoker)(this.ResultCountBoxUpdate));
            else
            {
                this.ResultCountBox.Text = this._searchResult.Count.ToString();
                this.ResultCountBox.Refresh();
            }
        }

        private void StopSearchButtonClick(object sender, EventArgs e)
        {
            this._rtm.StopSearch = true;
        }

        private void FixTheAddresses(object sender, EventArgs e)
        {
            try
            {
                if (!Functions.IsHex(startRangeAddressTextBox.Text))
                {
                    if (!this.startRangeAddressTextBox.Text.Equals(""))
                        this.startRangeAddressTextBox.Text = uint.Parse(this.startRangeAddressTextBox.Text).ToString("X");
                }

                if (!Functions.IsHex(lengthRangeAddressTextBox.Text))
                {
                    if (!this.lengthRangeAddressTextBox.Text.Equals(""))
                        this.lengthRangeAddressTextBox.Text = uint.Parse(this.lengthRangeAddressTextBox.Text).ToString("X");

                }

                uint value = Convert.ToUInt32(this.startRangeAddressTextBox.Text, 16);
                uint valueTwo = Convert.ToUInt32(this.lengthRangeAddressTextBox.Text, 16);
                this.totalTextBoxText.Text = (value+valueTwo).ToString("X");
            }
            catch (Exception ex)
            {
                this.ShowMessageBox(ex.Message, "PeekNPoke", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SearchRangeValueTextBoxLeave(object sender, EventArgs e)
        {
            this.searchRangeValueTextBox.Text = this.searchRangeValueTextBox.Text.Replace(" ", "");
        }

        private void resultGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                Clipboard.SetText(string.Format("" + this.resultGrid.Rows[this.resultGrid.SelectedRows[0].Index].Cells[1].Value));
                e.SuppressKeyPress = true;
            }
        }

        private void ResultCopy(object sender, EventArgs e)
        {
            if (this.resultGrid.Rows.Count == 0)
                return;
            Clipboard.SetText(string.Format("" + this.resultGrid.Rows[this.resultGrid.SelectedRows[0].Index].Cells[1].Value));
        }

        private void searchRangeValueTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char) 13)
            {
                try
                {
                    Thread oThread = new Thread(this.SearchRange);
                    oThread.Start();
                }
                catch (Exception ex)
                {
                    this.ShowMessageBox(ex.Message, string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
