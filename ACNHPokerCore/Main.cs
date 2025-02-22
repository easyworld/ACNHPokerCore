﻿using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace ACNHPokerCore
{
    #region event
    public delegate void CloseHandler();
    public delegate void OverrideHandler();
    public delegate void ValidationHandler();
    public delegate void SoundHandler(bool SoundOn);
    public delegate void ReceiveVariationHandler(inventorySlot item, int type);
    public delegate void ThreadAbortHandler();
    #endregion

    public partial class Main : Form
    {
        #region variable
        private static bool DEBUGGING = false;

        private static Socket socket;
        private static USBBot usb = null;
        private string version = "ACNHPokerCore R21 for v2.0.5";

        private Panel currentPanel;

        private static DataTable itemSource = null;
        private static DataTable recipeSource = null;
        private static DataTable flowerSource = null;
        private DataTable favSource = null;
        private static DataTable variationSource = null;
        private static Dictionary<string, string> OverrideDict;

        private DataGridViewRow lastRow;
        private DataGridViewRow recipelastRow;
        private DataGridViewRow flowerlastRow;
        private DataGridViewRow favlastRow;

        private Setting setting;

        private variation selection = null;
        private map M = null;
        private MapRegenerator R = null;
        private Freezer F = null;
        private Bulldozer B = null;
        private dodo D = null;
        private teleport T = null;
        private controller C = null;


        private inventorySlot selectedButton;
        private int selectedSlot = 1;
        private int counter = 0;

        private static byte[] header;
        private string IslandName = "";
        private int CurrentPlayerIndex = 0;
        private Villager[] V = null;
        private int[] HouseList;
        private Button[] villagerButton = null;
        private Button selectedVillagerButton = null;

        private bool VillagerFirstLoad = true;
        private bool VillagerLoading = false;

        private bool overrideSetting = false;
        private bool validation = true;
        public bool sound = true;
        private string languageSetting = "eng";

        private const string insectAppearFileName = @"InsectAppearParam.bin";
        private const string fishRiverAppearFileName = @"FishAppearRiverParam.bin";
        private const string fishSeaAppearFileName = @"FishAppearSeaParam.bin";
        private const string CreatureSeaAppearFileName = @"CreatureAppearSeaParam.bin";
        static private byte[] InsectAppearParam = LoadBinaryFile(insectAppearFileName);
        static private byte[] FishRiverAppearParam = LoadBinaryFile(fishRiverAppearFileName);
        static private byte[] FishSeaAppearParam = LoadBinaryFile(fishSeaAppearFileName);
        static private byte[] CreatureSeaAppearParam = LoadBinaryFile(CreatureSeaAppearFileName);
        private int[] insectRate;
        private int[] riverFishRate;
        private int[] seaFishRate;
        private int[] seaCreatureRate;
        private DataGridView currentGridView;

        private bool offline = true;
        private bool AllowInventoryUpdate = true;
        private bool ChineseFlag = false;

        private int maxPage = 1;
        private int currentPage = 1;

        private static Object itemLock = new Object();
        private static Object villagerLock = new Object();

        private WaveOut waveOut;
        #endregion


        public Main()
        {
            InitializeComponent();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            this.Text = version;

            this.IPAddressInputBox.Text = ConfigurationManager.AppSettings["ipAddress"];
            IPAddressInputBox.SelectionAlignment = HorizontalAlignment.Center;

            if (ConfigurationManager.AppSettings["override"] == "true")
            {
                overrideSetting = true;
                EasterEggButton.BackColor = Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            }

            if (ConfigurationManager.AppSettings["validation"] == "false")
            {
                validation = false;
            }

            if (ConfigurationManager.AppSettings["Sound"] == "false")
            {
                sound = false;
            }

            setting = new Setting(overrideSetting, validation, sound);
            setting.toggleOverride += Setting_toggleOverride;
            setting.toggleValidation += Setting_toggleValidation;
            setting.toggleSound += Setting_toggleSound;
            if (overrideSetting)
                setting.overrideAddresses();

            Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath.Replace(".exe", ".dll"));

            if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\" + "img") || ConfigurationManager.AppSettings["ForcedImageDownload"] == "true")
            {
                config.AppSettings.Settings["ForcedImageDownload"].Value = "false";
                config.Save(ConfigurationSaveMode.Minimal);
                ImgRetriever MoonMoon = new();
                MoonMoon.ShowDialog();
            }

            if (config.AppSettings.Settings["RestartRequired"].Value == "true")
            {
                config.AppSettings.Settings["RestartRequired"].Value = "false";
                config.Save(ConfigurationSaveMode.Minimal);
                Application.Restart();
            }

            if (File.Exists(Utilities.itemPath))
            {
                //load the csv
                itemSource = loadItemCSV(Utilities.itemPath);
                ItemGridView.DataSource = itemSource;

                //set the ID row invisible
                ItemGridView.Columns["id"].Visible = false;
                ItemGridView.Columns["iName"].Visible = false;
                ItemGridView.Columns["jpn"].Visible = false;
                ItemGridView.Columns["tchi"].Visible = false;
                ItemGridView.Columns["schi"].Visible = false;
                ItemGridView.Columns["kor"].Visible = false;
                ItemGridView.Columns["fre"].Visible = false;
                ItemGridView.Columns["ger"].Visible = false;
                ItemGridView.Columns["spa"].Visible = false;
                ItemGridView.Columns["ita"].Visible = false;
                ItemGridView.Columns["dut"].Visible = false;
                ItemGridView.Columns["rus"].Visible = false;
                ItemGridView.Columns["color"].Visible = false;
                ItemGridView.Columns["size"].Visible = false;

                ItemGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                ItemGridView.DefaultCellStyle.BackColor = Color.FromArgb(255, 47, 49, 54);
                ItemGridView.DefaultCellStyle.ForeColor = Color.FromArgb(255, 114, 105, 110);
                ItemGridView.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 57, 60, 67);

                ItemGridView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(255, 57, 60, 67);
                ItemGridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                ItemGridView.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 57, 60, 67);

                ItemGridView.EnableHeadersVisualStyles = false;

                //create the image column
                DataGridViewImageColumn imageColumn = new()
                {
                    Name = "Image",
                    HeaderText = "Image",
                    ImageLayout = DataGridViewImageCellLayout.Zoom
                };
                ItemGridView.Columns.Insert(13, imageColumn);
                imageColumn.SortMode = DataGridViewColumnSortMode.NotSortable;

                ItemGridView.Columns["eng"].Width = 195;
                ItemGridView.Columns["jpn"].Width = 195;
                ItemGridView.Columns["tchi"].Width = 195;
                ItemGridView.Columns["schi"].Width = 195;
                ItemGridView.Columns["kor"].Width = 195;
                ItemGridView.Columns["fre"].Width = 195;
                ItemGridView.Columns["ger"].Width = 195;
                ItemGridView.Columns["spa"].Width = 195;
                ItemGridView.Columns["ita"].Width = 195;
                ItemGridView.Columns["dut"].Width = 195;
                ItemGridView.Columns["rus"].Width = 195;
                ItemGridView.Columns["Image"].Width = 128;

                ItemGridView.Columns["eng"].HeaderText = "Name";
                ItemGridView.Columns["jpn"].HeaderText = "Name";
                ItemGridView.Columns["tchi"].HeaderText = "Name";
                ItemGridView.Columns["schi"].HeaderText = "Name";
                ItemGridView.Columns["kor"].HeaderText = "Name";
                ItemGridView.Columns["fre"].HeaderText = "Name";
                ItemGridView.Columns["ger"].HeaderText = "Name";
                ItemGridView.Columns["spa"].HeaderText = "Name";
                ItemGridView.Columns["ita"].HeaderText = "Name";
                ItemGridView.Columns["dut"].HeaderText = "Name";
                ItemGridView.Columns["rus"].HeaderText = "Name";

                ItemGridView.DefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            }
            else
            {
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
                MyMessageBox.Show("[Warning] Missing items.csv file!", "Missing CSV file!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (File.Exists(Utilities.overridePath))
            {
                OverrideDict = CreateOverride(Utilities.overridePath);
            }

            if (File.Exists(Utilities.recipePath))
            {
                recipeSource = loadItemCSV(Utilities.recipePath);
                RecipeGridView.DataSource = recipeSource;

                RecipeGridView.Columns["id"].Visible = false;
                RecipeGridView.Columns["iName"].Visible = false;
                RecipeGridView.Columns["jpn"].Visible = false;
                RecipeGridView.Columns["tchi"].Visible = false;
                RecipeGridView.Columns["schi"].Visible = false;
                RecipeGridView.Columns["kor"].Visible = false;
                RecipeGridView.Columns["fre"].Visible = false;
                RecipeGridView.Columns["ger"].Visible = false;
                RecipeGridView.Columns["spa"].Visible = false;
                RecipeGridView.Columns["ita"].Visible = false;
                RecipeGridView.Columns["dut"].Visible = false;
                RecipeGridView.Columns["rus"].Visible = false;

                RecipeGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                RecipeGridView.DefaultCellStyle.BackColor = Color.FromArgb(255, 47, 49, 54);
                RecipeGridView.DefaultCellStyle.ForeColor = Color.FromArgb(255, 114, 105, 110);
                RecipeGridView.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 57, 60, 67);

                RecipeGridView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(255, 57, 60, 67);
                RecipeGridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                RecipeGridView.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 57, 60, 67);

                RecipeGridView.EnableHeadersVisualStyles = false;

                DataGridViewImageColumn recipeimageColumn = new DataGridViewImageColumn
                {
                    Name = "Image",
                    HeaderText = "Image",
                    ImageLayout = DataGridViewImageCellLayout.Zoom
                };

                RecipeGridView.Columns.Insert(13, recipeimageColumn);
                recipeimageColumn.SortMode = DataGridViewColumnSortMode.NotSortable;

                RecipeGridView.Columns["eng"].Width = 195;
                RecipeGridView.Columns["jpn"].Width = 195;
                RecipeGridView.Columns["tchi"].Width = 195;
                RecipeGridView.Columns["schi"].Width = 195;
                RecipeGridView.Columns["kor"].Width = 195;
                RecipeGridView.Columns["fre"].Width = 195;
                RecipeGridView.Columns["ger"].Width = 195;
                RecipeGridView.Columns["spa"].Width = 195;
                RecipeGridView.Columns["ita"].Width = 195;
                RecipeGridView.Columns["dut"].Width = 195;
                RecipeGridView.Columns["rus"].Width = 195;
                RecipeGridView.Columns["Image"].Width = 128;

                RecipeGridView.Columns["eng"].HeaderText = "Name";
                RecipeGridView.Columns["jpn"].HeaderText = "Name";
                RecipeGridView.Columns["tchi"].HeaderText = "Name";
                RecipeGridView.Columns["schi"].HeaderText = "Name";
                RecipeGridView.Columns["kor"].HeaderText = "Name";
                RecipeGridView.Columns["fre"].HeaderText = "Name";
                RecipeGridView.Columns["ger"].HeaderText = "Name";
                RecipeGridView.Columns["spa"].HeaderText = "Name";
                RecipeGridView.Columns["ita"].HeaderText = "Name";
                RecipeGridView.Columns["dut"].HeaderText = "Name";
                RecipeGridView.Columns["rus"].HeaderText = "Name";

                RecipeGridView.DefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            }
            else
            {
                RecipeModeButton.Visible = false;
                RecipeGridView.Visible = false;
            }

            if (File.Exists(Utilities.flowerPath))
            {
                flowerSource = loadItemCSV(Utilities.flowerPath);
                FlowerGridView.DataSource = flowerSource;

                FlowerGridView.Columns["id"].Visible = false;
                FlowerGridView.Columns["iName"].Visible = false;
                FlowerGridView.Columns["jpn"].Visible = false;
                FlowerGridView.Columns["tchi"].Visible = false;
                FlowerGridView.Columns["schi"].Visible = false;
                FlowerGridView.Columns["kor"].Visible = false;
                FlowerGridView.Columns["fre"].Visible = false;
                FlowerGridView.Columns["ger"].Visible = false;
                FlowerGridView.Columns["spa"].Visible = false;
                FlowerGridView.Columns["ita"].Visible = false;
                FlowerGridView.Columns["dut"].Visible = false;
                FlowerGridView.Columns["rus"].Visible = false;
                FlowerGridView.Columns["value"].Visible = false;

                FlowerGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                FlowerGridView.DefaultCellStyle.BackColor = Color.FromArgb(255, 47, 49, 54);
                FlowerGridView.DefaultCellStyle.ForeColor = Color.FromArgb(255, 114, 105, 110);
                FlowerGridView.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 57, 60, 67);

                FlowerGridView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(255, 57, 60, 67);
                FlowerGridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                FlowerGridView.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 57, 60, 67);

                FlowerGridView.EnableHeadersVisualStyles = false;

                DataGridViewImageColumn flowerimageColumn = new DataGridViewImageColumn
                {
                    Name = "Image",
                    HeaderText = "Image",
                    ImageLayout = DataGridViewImageCellLayout.Zoom
                };

                FlowerGridView.Columns.Insert(13, flowerimageColumn);
                flowerimageColumn.SortMode = DataGridViewColumnSortMode.NotSortable;

                FlowerGridView.Columns["eng"].Width = 195;
                FlowerGridView.Columns["jpn"].Width = 195;
                FlowerGridView.Columns["tchi"].Width = 195;
                FlowerGridView.Columns["schi"].Width = 195;
                FlowerGridView.Columns["kor"].Width = 195;
                FlowerGridView.Columns["fre"].Width = 195;
                FlowerGridView.Columns["ger"].Width = 195;
                FlowerGridView.Columns["spa"].Width = 195;
                FlowerGridView.Columns["ita"].Width = 195;
                FlowerGridView.Columns["dut"].Width = 195;
                FlowerGridView.Columns["rus"].Width = 195;
                FlowerGridView.Columns["Image"].Width = 128;

                FlowerGridView.Columns["eng"].HeaderText = "Name";
                FlowerGridView.Columns["jpn"].HeaderText = "Name";
                FlowerGridView.Columns["tchi"].HeaderText = "Name";
                FlowerGridView.Columns["schi"].HeaderText = "Name";
                FlowerGridView.Columns["kor"].HeaderText = "Name";
                FlowerGridView.Columns["fre"].HeaderText = "Name";
                FlowerGridView.Columns["ger"].HeaderText = "Name";
                FlowerGridView.Columns["spa"].HeaderText = "Name";
                FlowerGridView.Columns["ita"].HeaderText = "Name";
                FlowerGridView.Columns["dut"].HeaderText = "Name";
                FlowerGridView.Columns["rus"].HeaderText = "Name";

                FlowerGridView.DefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            }
            else
            {
                FlowerModeButton.Visible = false;
                FlowerGridView.Visible = false;
            }

            if (File.Exists(Utilities.variationPath))
            {
                variationSource = loadItemCSV(Utilities.variationPath);
            }
            else
            {
                VariationButton.Visible = false;
            }

            if (!File.Exists(Utilities.favPath))
            {
                string favheader = "id" + " ; " + "iName" + " ; " + "Name" + " ; " + "value" + " ; ";

                string directoryPath = Directory.GetCurrentDirectory() + "\\" + Utilities.csvFolder;

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                using (StreamWriter sw = File.CreateText(Utilities.favPath))
                {
                    sw.WriteLine(favheader);
                }
            }

            if (File.Exists(Utilities.favPath))
            {
                favSource = loadCSVwoKey(Utilities.favPath);
                FavGridView.DataSource = favSource;

                FavGridView.Columns["id"].Visible = false;
                FavGridView.Columns["iName"].Visible = false;
                FavGridView.Columns["value"].Visible = false;

                FavGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                FavGridView.DefaultCellStyle.BackColor = Color.FromArgb(255, 47, 49, 54);
                FavGridView.DefaultCellStyle.ForeColor = Color.FromArgb(255, 114, 105, 110);
                FavGridView.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 57, 60, 67);

                FavGridView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(255, 57, 60, 67);
                FavGridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                FavGridView.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 57, 60, 67);

                FavGridView.EnableHeadersVisualStyles = false;

                DataGridViewImageColumn favimageColumn = new DataGridViewImageColumn
                {
                    Name = "Image",
                    HeaderText = "Image",
                    ImageLayout = DataGridViewImageCellLayout.Zoom
                };
                FavGridView.Columns.Insert(4, favimageColumn);
                favimageColumn.SortMode = DataGridViewColumnSortMode.NotSortable;

                FavGridView.Columns["Name"].Width = 195;
                FavGridView.Columns["Image"].Width = 128;

                FavGridView.DefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            }

            currentPanel = ItemModePanel;

            LanguageSetup(config.AppSettings.Settings["language"].Value);

            Utilities.buildDictionary();

            this.KeyPreview = true;

            if (DEBUGGING)
            {
                this.MapDropperButton.Visible = true;
                this.RegeneratorButton.Visible = true;
                this.FreezerButton.Visible = true;
                this.DodoHelperButton.Visible = true;
                this.BulldozerButton.Visible = true;
            }
        }

        private void Setting_toggleSound(bool SoundOn)
        {
            sound = SoundOn;
        }

        private void Setting_toggleValidation()
        {
            if (validation == true)
                validation = false;
            else
                validation = true;
        }

        private void Setting_toggleOverride()
        {
            if (overrideSetting == true)
            {
                overrideSetting = false;
                EasterEggButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            }
            else
            {
                overrideSetting = true;
                EasterEggButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            }
        }

        private void SettingButton_Click(object sender, EventArgs e)
        {
            setting.ShowDialog();
        }

        #region Load File
        private static DataTable loadItemCSV(string filePath)
        {
            var dt = new DataTable();

            File.ReadLines(filePath).Take(1)
                .SelectMany(x => x.Split(new[] { " ; " }, StringSplitOptions.RemoveEmptyEntries))
                .ToList()
                .ForEach(x => dt.Columns.Add(x.Trim()));

            File.ReadLines(filePath).Skip(1)
                .Select(x => x.Split(new[] { " ; " }, StringSplitOptions.RemoveEmptyEntries))
                .ToList()
                .ForEach(line => dt.Rows.Add(line));

            if (dt.Columns.Contains("id"))
                dt.PrimaryKey = new DataColumn[1] { dt.Columns["id"] };

            return dt;
        }

        private static DataTable loadCSVwoKey(string filePath)
        {
            var dt = new DataTable();

            File.ReadLines(filePath).Take(1)
                .SelectMany(x => x.Split(new[] { " ; " }, StringSplitOptions.RemoveEmptyEntries))
                .ToList()
                .ForEach(x => dt.Columns.Add(x.Trim()));

            File.ReadLines(filePath).Skip(1)
                .Select(x => x.Split(new[] { " ; " }, StringSplitOptions.RemoveEmptyEntries))
                .ToList()
                .ForEach(line => dt.Rows.Add(line));

            return dt;
        }

        static private byte[] LoadBinaryFile(string file)
        {
            if (File.Exists(file))
            {
                return File.ReadAllBytes(file);
            }
            else return null;
        }
        private static Dictionary<string, string> CreateOverride(string path)
        {
            Dictionary<string, string> dict = new();

            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path);

                foreach (string line in lines)
                {
                    string[] parts = line.Split(new[] { " ; " }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 3)
                    {
                        dict.Add(parts[1], parts[2]);
                    }
                }
            }

            return dict;
        }

        #endregion

        private void ItemGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < this.ItemGridView.Rows.Count)
            {
                if (e.ColumnIndex == 13)
                {
                    string path;
                    if (ItemGridView.Rows[e.RowIndex].Cells["iName"].Value == null)
                        return;

                    var value = ItemGridView.Rows[e.RowIndex].Cells["iName"].Value;
                    if (value == null)
                        return;
                    string imageName = value.ToString();

                    if (OverrideDict.ContainsKey(imageName))
                    {
                        path = Utilities.imagePath + OverrideDict[imageName] + ".png";
                        if (File.Exists(path))
                        {
                            Image img = Image.FromFile(path);
                            //e.CellStyle.BackColor = Color.Green;
                            e.Value = img;

                            return;
                        }
                    }

                    path = Utilities.imagePath + imageName + "_Remake_0_0.png";
                    if (File.Exists(path))
                    {
                        Image img = Image.FromFile(path);
                        e.CellStyle.BackColor = Color.FromArgb(((int)(((byte)(56)))), ((int)(((byte)(77)))), ((int)(((byte)(162)))));
                        e.Value = img;
                    }
                    else
                    {
                        path = Utilities.imagePath + removeNumber(imageName) + ".png";
                        if (File.Exists(path))
                        {
                            Image img = Image.FromFile(path);
                            e.Value = img;
                        }
                        else
                        {
                            path = Utilities.imagePath + imageName + ".png";
                            if (File.Exists(path))
                            {
                                Image img = Image.FromFile(path);
                                e.Value = img;
                            }
                            else
                            {
                                e.CellStyle.BackColor = Color.Red;
                            }
                        }
                    }
                }
            }
        }
        private static string removeNumber(string filename)
        {
            char[] MyChar = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
            return filename.Trim(MyChar);
        }

        private void ItemGridView_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            MouseEventArgs me = (MouseEventArgs)e;
            if (me.Button == System.Windows.Forms.MouseButtons.Left)
            {
                if (lastRow != null)
                {
                    lastRow.Height = 22;
                }


                string hexValue = "0";

                if (e.RowIndex > -1)
                {
                    lastRow = ItemGridView.Rows[e.RowIndex];
                    ItemGridView.Rows[e.RowIndex].Height = 128;
                    if (HexModeButton.Tag.ToString() == "Normal")
                    {
                        if (AmountOrCountTextbox.Text == "" || AmountOrCountTextbox.Text == "0")
                        {
                            AmountOrCountTextbox.Text = "1";
                        }

                        int decValue = Convert.ToInt32(AmountOrCountTextbox.Text) - 1;
                        if (decValue >= 0)
                            hexValue = decValue.ToString("X");
                    }
                    else
                    {
                        if (AmountOrCountTextbox.Text == "" || AmountOrCountTextbox.Text == "0")
                        {
                            AmountOrCountTextbox.Text = Utilities.precedingZeros("0", 8);
                        }

                        hexValue = AmountOrCountTextbox.Text;
                        //HexModeButton_Click(sender, e);
                        //AmountOrCountTextbox.Text = "1";
                    }


                    string id = ItemGridView.Rows[e.RowIndex].Cells["id"].Value.ToString();
                    string name = ItemGridView.Rows[e.RowIndex].Cells[languageSetting].Value.ToString();

                    IDTextbox.Text = id;

                    UInt16 IntId = Convert.ToUInt16(id, 16);

                    string front = Utilities.precedingZeros(hexValue, 8).Substring(0, 4);
                    string back = Utilities.precedingZeros(hexValue, 8).Substring(4, 4);

                    if (ItemAttr.hasFenceWithVariation(IntId))  // Fence Variation
                    {
                        SelectedItem.setup(name, Convert.ToUInt16("0x" + id, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(id, itemSource, Convert.ToUInt32("0x" + front, 16)), true, "");
                    }
                    else
                    {
                        SelectedItem.setup(name, Convert.ToUInt16("0x" + id, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(id, itemSource, Convert.ToUInt32("0x" + hexValue, 16)), true, "");
                    }

                    if (selection != null)
                    {
                        selection.receiveID(Utilities.precedingZeros(SelectedItem.fillItemID(), 4), languageSetting, Utilities.precedingZeros(hexValue, 8));
                    }
                    updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
                }
            }
            else if (me.Button == System.Windows.Forms.MouseButtons.Right)
            {
                if (lastRow != null)
                {
                    lastRow.Height = 22;
                }

                if (e.RowIndex > -1)
                {
                    lastRow = ItemGridView.Rows[e.RowIndex];
                    ItemGridView.Rows[e.RowIndex].Height = 128;

                    string name = SelectedItem.displayItemName();
                    string id = SelectedItem.displayItemID();
                    string path = SelectedItem.getPath();

                    if (HexModeButton.Tag.ToString() == "Normal")
                    {
                        HexModeButton_Click(sender, e);
                    }
                    else
                    {

                    }

                    if (IDTextbox.Text != "")
                    {
                        if (IDTextbox.Text == "315A" || IDTextbox.Text == "1618" || IDTextbox.Text == "342F") // Wall-Mounted
                        {
                            AmountOrCountTextbox.Text = Utilities.precedingZeros(ItemGridView.Rows[e.RowIndex].Cells["id"].Value.ToString(), 8);
                            SelectedItem.setup(name, Convert.ToUInt16(id, 16), Convert.ToUInt32("0x" + AmountOrCountTextbox.Text, 16), path, true, GetImagePathFromID(ItemGridView.Rows[e.RowIndex].Cells["id"].Value.ToString(), itemSource));
                        }
                        else
                        {
                            AmountOrCountTextbox.Text = Utilities.precedingZeros(ItemGridView.Rows[e.RowIndex].Cells["id"].Value.ToString(), 8);
                            SelectedItem.setup(name, Convert.ToUInt16(id, 16), Convert.ToUInt32("0x" + AmountOrCountTextbox.Text, 16), path, true, GetNameFromID(ItemGridView.Rows[e.RowIndex].Cells["id"].Value.ToString(), itemSource));
                        }

                        if (selection != null)
                        {
                            selection.receiveID(Utilities.turn2bytes(SelectedItem.fillItemData()), languageSetting);
                        }

                        updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
                    }

                }
            }
        }

        private void HexModeButton_Click(object sender, EventArgs e)
        {
            if (HexModeButton.Tag.ToString() == "Normal")
            {
                AmountOrCountLabel.Text = "Hex Value";
                HexModeButton.Tag = "Hex";
                HexModeButton.Text = "Normal Mode";
                if (AmountOrCountTextbox.Text != "")
                {
                    int decValue = Convert.ToInt32(AmountOrCountTextbox.Text) - 1;
                    string hexValue;
                    if (decValue < 0)
                        hexValue = "0";
                    else
                        hexValue = decValue.ToString("X");
                    AmountOrCountTextbox.Text = Utilities.precedingZeros(hexValue, 8);
                }
            }
            else
            {
                AmountOrCountLabel.Text = "Amount";
                HexModeButton.Tag = "Normal";
                HexModeButton.Text = "Hex Mode";
                if (AmountOrCountTextbox.Text != "")
                {
                    string hexValue = AmountOrCountTextbox.Text;
                    int decValue = Convert.ToInt32(hexValue, 16) + 1;
                    AmountOrCountTextbox.Text = decValue.ToString();
                }
            }
        }

        public static string GetImagePathFromID(string itemID, DataTable source, UInt32 data = 0)
        {
            if (source == null)
            {
                return "";
            }

            DataRow row = source.Rows.Find(itemID);
            DataRow VarRow = null;
            if (variationSource != null)
                VarRow = variationSource.Rows.Find(itemID);

            if (row == null)
            {
                return ""; //row not found
            }
            else
            {

                string path;
                if (VarRow != null & source != recipeSource)
                {
                    string main = (data & 0xF).ToString();
                    string sub = (((data & 0xFF) - (data & 0xF)) / 0x20).ToString();
                    //Debug.Print("data " + data.ToString("X") + " Main " + main + " Sub " + sub);
                    path = Utilities.imagePath + VarRow["iName"] + "_Remake_" + main + "_" + sub + ".png";
                    if (File.Exists(path))
                    {
                        return path;
                    }

                    path = Utilities.imagePath + VarRow["iName"] + ".png";
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }

                string imageName = row[1].ToString();

                if (OverrideDict.ContainsKey(imageName))
                {
                    path = Utilities.imagePath + OverrideDict[imageName] + ".png";
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }

                path = Utilities.imagePath + imageName + "_Remake_0_0.png";
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    path = Utilities.imagePath + imageName + ".png";
                    if (File.Exists(path))
                    {
                        return path;
                    }
                    else
                    {
                        path = Utilities.imagePath + removeNumber(imageName) + ".png";
                        if (File.Exists(path))
                        {
                            return path;
                        }
                        else
                        {
                            return "";
                        }
                    }
                }
            }
        }

        public static string GetImagePathFromID(string itemID, UInt32 data)
        {
            return GetImagePathFromID(itemID, itemSource, data);
        }

        public string GetNameFromID(string itemID, DataTable source)
        {
            if (source == null)
            {
                return "";
            }

            DataRow row = source.Rows.Find(itemID);

            if (row == null)
            {
                return ""; //row not found
            }
            else
            {
                //row found set the index and find the name
                return (string)row[languageSetting];
            }
        }

        public void updateSelectedItemInfo(string Name, string ID, string Data)
        {
            SelectedItemName.Text = Name;
            selectedID.Text = ID;
            selectedData.Text = Data;
            selectedFlag1.Text = SelectedItem.getFlag1();
            selectedFlag2.Text = SelectedItem.getFlag2();
        }

        private string UpdateTownID()
        {
            if (socket == null && usb == null)
                return "";
            byte[] townID = Utilities.GetTownID(socket, usb);
            IslandName = Utilities.GetString(townID, 0x04, 10);
            return "  |  Island Name : " + IslandName;
        }

        private void LanguageSetup(string configLanguage)
        {
            switch (configLanguage)
            {
                case "eng":
                    LanguageSelector.SelectedIndex = 0;
                    break;
                case "jpn":
                    LanguageSelector.SelectedIndex = 1;
                    break;
                case "tchi":
                    LanguageSelector.SelectedIndex = 2;
                    break;
                case "schi":
                    LanguageSelector.SelectedIndex = 3;
                    break;
                case "kor":
                    LanguageSelector.SelectedIndex = 4;
                    break;
                case "fre":
                    LanguageSelector.SelectedIndex = 5;
                    break;
                case "ger":
                    LanguageSelector.SelectedIndex = 6;
                    break;
                case "spa":
                    LanguageSelector.SelectedIndex = 7;
                    break;
                case "ita":
                    LanguageSelector.SelectedIndex = 8;
                    break;
                case "dut":
                    LanguageSelector.SelectedIndex = 9;
                    break;
                case "rus":
                    LanguageSelector.SelectedIndex = 10;
                    break;
                default:
                    LanguageSelector.SelectedIndex = 0;
                    break;
            }
        }

        private void LanguageSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ItemSearchBox.Text != "Search")
            {
                ItemSearchBox.Clear();
            }

            Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath.Replace(".exe", ".dll"));

            switch (LanguageSelector.SelectedIndex)
            {
                case 0:
                    languageSetting = "eng";
                    config.AppSettings.Settings["language"].Value = "eng";
                    break;
                case 1:
                    languageSetting = "jpn";
                    config.AppSettings.Settings["language"].Value = "jpn";
                    break;
                case 2:
                    languageSetting = "tchi";
                    config.AppSettings.Settings["language"].Value = "tchi";
                    break;
                case 3:
                    languageSetting = "schi";
                    config.AppSettings.Settings["language"].Value = "schi";
                    break;
                case 4:
                    languageSetting = "kor";
                    config.AppSettings.Settings["language"].Value = "kor";
                    break;
                case 5:
                    languageSetting = "fre";
                    config.AppSettings.Settings["language"].Value = "fre";
                    break;
                case 6:
                    languageSetting = "ger";
                    config.AppSettings.Settings["language"].Value = "ger";
                    break;
                case 7:
                    languageSetting = "spa";
                    config.AppSettings.Settings["language"].Value = "spa";
                    break;
                case 8:
                    languageSetting = "ita";
                    config.AppSettings.Settings["language"].Value = "ita";
                    break;
                case 9:
                    languageSetting = "dut";
                    config.AppSettings.Settings["language"].Value = "dut";
                    break;
                case 10:
                    languageSetting = "rus";
                    config.AppSettings.Settings["language"].Value = "rus";
                    break;
                default:
                    languageSetting = "eng";
                    config.AppSettings.Settings["language"].Value = "eng";
                    break;
            }

            config.Save(ConfigurationSaveMode.Minimal);

            if (ItemGridView.Columns.Contains(languageSetting))
            {
                hideAllLanguage();
                ItemGridView.Columns[languageSetting].Visible = true;
                RecipeGridView.Columns[languageSetting].Visible = true;
                FlowerGridView.Columns[languageSetting].Visible = true;
            }
        }

        private void hideAllLanguage()
        {
            if (ItemGridView.Columns.Contains("id"))
            {
                ItemGridView.Columns["eng"].Visible = false;
                ItemGridView.Columns["jpn"].Visible = false;
                ItemGridView.Columns["tchi"].Visible = false;
                ItemGridView.Columns["schi"].Visible = false;
                ItemGridView.Columns["kor"].Visible = false;
                ItemGridView.Columns["fre"].Visible = false;
                ItemGridView.Columns["ger"].Visible = false;
                ItemGridView.Columns["spa"].Visible = false;
                ItemGridView.Columns["ita"].Visible = false;
                ItemGridView.Columns["dut"].Visible = false;
                ItemGridView.Columns["rus"].Visible = false;
            }

            if (RecipeGridView.Columns.Contains("id"))
            {
                RecipeGridView.Columns["eng"].Visible = false;
                RecipeGridView.Columns["jpn"].Visible = false;
                RecipeGridView.Columns["tchi"].Visible = false;
                RecipeGridView.Columns["schi"].Visible = false;
                RecipeGridView.Columns["kor"].Visible = false;
                RecipeGridView.Columns["fre"].Visible = false;
                RecipeGridView.Columns["ger"].Visible = false;
                RecipeGridView.Columns["spa"].Visible = false;
                RecipeGridView.Columns["ita"].Visible = false;
                RecipeGridView.Columns["dut"].Visible = false;
                RecipeGridView.Columns["rus"].Visible = false;
            }

            if (FlowerGridView.Columns.Contains("id"))
            {
                FlowerGridView.Columns["eng"].Visible = false;
                FlowerGridView.Columns["jpn"].Visible = false;
                FlowerGridView.Columns["tchi"].Visible = false;
                FlowerGridView.Columns["schi"].Visible = false;
                FlowerGridView.Columns["kor"].Visible = false;
                FlowerGridView.Columns["fre"].Visible = false;
                FlowerGridView.Columns["ger"].Visible = false;
                FlowerGridView.Columns["spa"].Visible = false;
                FlowerGridView.Columns["ita"].Visible = false;
                FlowerGridView.Columns["dut"].Visible = false;
                FlowerGridView.Columns["rus"].Visible = false;
            }
        }

        #region Easter Egg
        private void EasterEggButton_Click(object sender, EventArgs e)
        {
            if (waveOut == null)
            {
                MyLog.logEvent("MainForm", "EasterEgg Started");

                Thread songThread = new Thread(delegate () { egg(); });
                songThread.Start();
            }
            else
            {
                MyLog.logEvent("MainForm", "EasterEgg Stopped");
                waveOut.Stop();
                waveOut.Dispose();
                waveOut = null;
            }
        }

        private void egg()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory;
            AudioFileReader audioFileReader = new AudioFileReader(path + Utilities.villagerPath + "Io.nhv2");
            LoopStream loop = new LoopStream(audioFileReader);
            waveOut = new WaveOut();
            waveOut.Init(loop);
            waveOut.Play();
        }

        #endregion

        #region Tab
        private void InventoryTabButton_Paint(object sender, PaintEventArgs e)
        {
            Font font = new Font("Arial", 10, FontStyle.Bold);
            Brush brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            e.Graphics.TranslateTransform(21, 7);
            e.Graphics.RotateTransform(90);
            e.Graphics.DrawString("Inventory", font, brush, 0, 0);
        }

        private void OtherTabButton_Paint(object sender, PaintEventArgs e)
        {
            Font font = new Font("Arial", 10, FontStyle.Bold);
            Brush brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            e.Graphics.TranslateTransform(21, 14);
            e.Graphics.RotateTransform(90);
            e.Graphics.DrawString("Other", font, brush, 0, 0);
        }

        private void CritterTabButton_Paint(object sender, PaintEventArgs e)
        {
            Font font = new Font("Arial", 10, FontStyle.Bold);
            Brush brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            e.Graphics.TranslateTransform(21, 14);
            e.Graphics.RotateTransform(90);
            e.Graphics.DrawString("Critter", font, brush, 0, 0);
        }

        private void VillagerTabButton_Paint(object sender, PaintEventArgs e)
        {
            Font font = new Font("Arial", 10, FontStyle.Bold);
            Brush brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            e.Graphics.TranslateTransform(21, 14);
            e.Graphics.RotateTransform(90);
            e.Graphics.DrawString("Villager", font, brush, 0, 0);
        }
        #endregion

        private void StartConnectionButton_Click(object sender, EventArgs e)
        {
            if (StartConnectionButton.Tag.ToString() == "connect")
            {
                string ipPattern = @"\b(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b";

                if (!Regex.IsMatch(IPAddressInputBox.Text, ipPattern))
                {
                    IPAddressInputBackground.BackColor = System.Drawing.Color.Orange;
                    return;
                }

                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(IPAddressInputBox.Text), 6000);

                if (socket.Connected == false)
                {
                    new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;


                        IAsyncResult result = socket.BeginConnect(ep, null, null);
                        bool conSuceded = result.AsyncWaitHandle.WaitOne(3000, true);


                        if (conSuceded == true)
                        {
                            try
                            {
                                socket.EndConnect(result);
                            }
                            catch
                            {
                                this.IPAddressInputBackground.Invoke((MethodInvoker)delegate
                                {
                                    MyLog.logEvent("MainForm", "Connection Failed : " + IPAddressInputBox.Text);
                                    this.IPAddressInputBackground.BackColor = Color.Red;
                                });
                                MyMessageBox.Show("You have successfully started a connection!\n" +
                                                "Your IP address seems to be correct!\n" +
                                                "However...\n" +
                                                "Sys-botbase is not responding...\n\n\n" +

                                                "First, \n" +
                                                "check that your Switch is running in CFW mode.\n" +
                                                "On your Switch, go to \"System Settings\"->\"System\"\n" +
                                                "Check \"Current version:\" and make sure you have \"AMS\" in it.\n\n" +
                                                "Second, \n" +
                                                "check that you are connecting to the correct IP address.\n" +
                                                "On your Switch, go to \"System Settings\"->\"Internet\"\n" +
                                                "Check the \"IP address\" under \"Connection status\"\n\n" +
                                                "Third, \n" +
                                                "check that you have the latest version of Sys-botbase installed.\n" +
                                                "You can get the latest version at \n        https://github.com/olliz0r/sys-botbase/releases \n" +
                                                "You should double check your installation and make sure that the \n \"430000000000000B\" folder is inside the \"atmosphere\\contents\" folder.\n" +
                                                "Also when your Switch is starting up,\n" +
                                                "please also check that the LED of the \"Home button\" on your attached Joy-Con is lighting up. "

                                                , "Where are you, my socket 6000?", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                                return;
                            }

                            Invoke((MethodInvoker)delegate
                            {
                                this.IPAddressInputBackground.BackColor = System.Drawing.Color.Green;

                                Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath.Replace(".exe", ".dll"));

                                config.AppSettings.Settings["ipAddress"].Value = this.IPAddressInputBox.Text;
                                config.Save(ConfigurationSaveMode.Minimal);
                                if (config.AppSettings.Settings["autoRefresh"].Value == "true")
                                {
                                    this.InventoryAutoRefreshToggle.Checked = true;
                                }
                                else
                                {
                                    this.InventoryAutoRefreshToggle.Checked = false;
                                }

                                MyLog.logEvent("MainForm", "Connection Succeeded : " + IPAddressInputBox.Text);

                                if (DataValidation())
                                {
                                    string sysbotbaseVersion = Utilities.CheckSysBotBase(socket, usb);
                                    MyMessageBox.Show("You have successfully established a connection!\n" +
                                                    "Your Sys-botbase installation and IP address are correct.\n" +
                                                    "However...\n" +
                                                    "Sys-botbase validation failed! Poke request is returning same result.\n\n\n" +

                                                    "First, \n" +
                                                    "check that you have the correct matching version.\n" +
                                                    "You are using \"" + version + "\" right now.\n" +
                                                    "You can find the latest version at : \n   https://github.com/MyShiLingStar/ACNHPoker/releases \n" +
                                                    "Your sys-botbase version is :  " + sysbotbaseVersion + "\n" +
                                                    "You can find the latest version at : \n   https://github.com/olliz0r/sys-botbase/releases \n\n" +
                                                    "Second, \n" +
                                                    "try holding the power button and restart your switch.\n" +
                                                    "Then press and HOLD the \"L\" button while you are selecting the game to boot up.\n" +
                                                    "Keep holding the \"L\" button and release it once you can see the title screen.\n" +
                                                    "Retry the connection.\n\n" +
                                                    "Third, \n" +
                                                    "If you use a cheat file before and have the following folder : \n" +
                                                    "sd: / atmospshere / contents / 01006F8002326000 /\n" +
                                                    "please try to remove or rename it.\n"

                                                    , "Sys-botbase validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    socket.Close();
                                    return;
                                }



                                this.RefreshButton.Visible = true;
                                this.PlayerInventorySelector.Visible = true;

                                this.InventoryAutoRefreshToggle.Visible = true;
                                this.AutoRefreshLabel.Visible = true;

                                this.OtherTabButton.Visible = true;
                                this.CritterTabButton.Visible = true;
                                this.VillagerTabButton.Visible = true;

                                this.WrapSelector.SelectedIndex = 0;

                                this.StartConnectionButton.Tag = "disconnect";
                                this.StartConnectionButton.Text = "Disconnect";
                                this.USBConnectionButton.Visible = false;
                                this.SettingButton.Visible = false;
                                this.MapDropperButton.Visible = true;
                                this.RegeneratorButton.Visible = true;
                                this.FreezerButton.Visible = true;
                                this.DodoHelperButton.Visible = true;
                                this.BulldozerButton.Visible = true;

                                offline = false;

                                CurrentPlayerIndex = updateDropdownBox();

                                PlayerInventorySelector.SelectedIndex = CurrentPlayerIndex;
                                PlayerInventorySelectorOther.SelectedIndex = CurrentPlayerIndex;
                                this.Text = this.Text + UpdateTownID();

                                setEatButton();
                                UpdateTurnipPrices();
                                readWeatherSeed();

                                currentGridView = InsectGridView;
                                LoadGridView(InsectAppearParam, InsectGridView, ref insectRate, Utilities.InsectDataSize, Utilities.InsectNumRecords);
                                LoadGridView(FishRiverAppearParam, RiverFishGridView, ref riverFishRate, Utilities.FishDataSize, Utilities.FishRiverNumRecords, 1);
                                LoadGridView(FishSeaAppearParam, SeaFishGridView, ref seaFishRate, Utilities.FishDataSize, Utilities.FishSeaNumRecords, 1);
                                LoadGridView(CreatureSeaAppearParam, SeaCreatureGridView, ref seaCreatureRate, Utilities.SeaCreatureDataSize, Utilities.SeaCreatureNumRecords, 1);

                                T = new teleport(socket);
                                C = new controller(socket, IslandName);
                            });

                        }
                        else
                        {
                            socket.Close();
                            this.IPAddressInputBackground.Invoke((MethodInvoker)delegate
                            {
                                this.IPAddressInputBackground.BackColor = System.Drawing.Color.Red;
                            });
                            MyMessageBox.Show("Unable to connect to the Sys-botbase server.\n" +
                                            "Please double check your IP address and Sys-botbase installation.\n\n" +
                                            "You might also need to disable your firewall or antivirus temporary to allow outgoing connection."
                                            , "Unable to establish connection!", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        }
                    }).Start();
                }
            }
            else
            {
                socket.Close();
                foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                {
                    btn.reset();
                }

                RefreshButton.Visible = false;
                PlayerInventorySelector.Visible = false;

                this.InventoryAutoRefreshToggle.Visible = false;
                this.AutoRefreshLabel.Visible = false;

                //this.USBConnectionButton.Visible = true;
                InventoryTabButton_Click(sender, e);
                this.OtherTabButton.Visible = false;
                this.CritterTabButton.Visible = false;
                this.VillagerTabButton.Visible = false;
                this.SettingButton.Visible = true;
                CleanVillagerPage();
                this.MapDropperButton.Visible = false;
                this.RegeneratorButton.Visible = false;
                this.FreezerButton.Visible = false;
                this.DodoHelperButton.Visible = false;
                this.BulldozerButton.Visible = false;

                offline = true;

                this.StartConnectionButton.Tag = "connect";
                this.StartConnectionButton.Text = "Connect";

                this.Text = version;
            }
        }

        #region Validation
        private Boolean DataValidation()
        {
            //return true;
            if (!validation)
                return false;
            try
            {
                byte[] Bank1 = Utilities.peekAddress(socket, usb, Utilities.TownNameddress, 150); //TownNameddress
                byte[] Bank2 = Utilities.peekAddress(socket, usb, Utilities.TurnipPurchasePriceAddr, 150); //TurnipPurchasePriceAddr
                byte[] Bank3 = Utilities.peekAddress(socket, usb, Utilities.MasterRecyclingBase, 150); //MasterRecyclingBase
                byte[] Bank4 = Utilities.peekAddress(socket, usb, Utilities.playerReactionAddress, 150); //reactionAddress
                byte[] Bank5 = Utilities.peekAddress(socket, usb, Utilities.staminaAddress, 150); //staminaAddress

                string HexString1 = Utilities.ByteToHexString(Bank1);
                string HexString2 = Utilities.ByteToHexString(Bank2);
                string HexString3 = Utilities.ByteToHexString(Bank3);
                string HexString4 = Utilities.ByteToHexString(Bank4);
                string HexString5 = Utilities.ByteToHexString(Bank5);

                Debug.Print(HexString1);
                Debug.Print(HexString2);
                Debug.Print(HexString3);
                Debug.Print(HexString4);
                Debug.Print(HexString5);

                int count1 = 0;
                if (HexString1 == HexString2)
                { count1++; }
                if (HexString1 == HexString3)
                { count1++; }
                if (HexString1 == HexString4)
                { count1++; }
                if (HexString1 == HexString5)
                { count1++; }

                int count2 = 0;
                if (HexString2 == HexString3)
                { count2++; }
                if (HexString2 == HexString4)
                { count2++; }
                if (HexString2 == HexString5)
                { count2++; }

                int count3 = 0;
                if (HexString3 == HexString4)
                { count3++; }
                if (HexString3 == HexString5)
                { count3++; }

                Debug.Print("Count : " + count1.ToString() + " " + count2.ToString() + " " + count3.ToString());
                if (count1 > 1 || count2 > 1 || count3 > 1)
                { return true; }
                else
                { return false; }
            }
            catch (Exception e)
            {
                MyMessageBox.Show(e.Message.ToString(), "Todo : this is dumb");
                return false;
            }
        }

        #endregion

        #region Inventory Name
        private string[] getInventoryName()
        {
            string[] namelist = new string[8];
            Debug.Print("Peek 8 Name:");
            byte[] tempHeader = null;
            Boolean headerFound = false;

            for (int i = 0; i < 8; i++)
            {
                byte[] b = Utilities.peekAddress(socket, usb, (uint)(Utilities.player1SlotBase + (i * Utilities.playerOffset)) + Utilities.InventoryNameOffset, 0x34);
                namelist[i] = Encoding.Unicode.GetString(b, 32, 20);
                namelist[i] = namelist[i].Replace("\0", string.Empty);
                if (namelist[i].Equals(string.Empty) && !headerFound)
                {
                    header = tempHeader;
                    headerFound = true;
                }
                tempHeader = b;
            }
            return namelist;
        }

        public static byte[] getHeader()
        {
            return header;
        }

        private int updateDropdownBox()
        {
            string[] namelist = getInventoryName();
            int currentPlayer = 0;
            for (int i = 7; i >= 0; i--)
            {
                if (namelist[i] != string.Empty)
                {
                    PlayerInventorySelector.Items.RemoveAt(i);
                    PlayerInventorySelector.Items.Insert(i, namelist[i]);
                    PlayerInventorySelector.Items.RemoveAt(i + 8);
                    PlayerInventorySelector.Items.Insert(i + 8, namelist[i] + "'s House");

                    PlayerInventorySelectorOther.Items.RemoveAt(i);
                    PlayerInventorySelectorOther.Items.Insert(i, namelist[i]);
                    if (i > currentPlayer)
                        currentPlayer = i;
                }
            }
            return currentPlayer;
        }
        #endregion

        #region Auto Refresh

        private void InventoryRefreshTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (socket != null && socket.Connected == true && InventoryAutoRefreshToggle.Checked && AllowInventoryUpdate)
                    Invoke((MethodInvoker)delegate
                    {
                        UpdateInventory();
                    });
            }
            catch (Exception ex)
            {
                MyLog.logEvent("MainForm", "RefreshTimer: " + ex.Message.ToString());
                Invoke((MethodInvoker)delegate { this.InventoryAutoRefreshToggle.Checked = false; });
                InventoryRefreshTimer.Stop();
                MyMessageBox.Show("Lost connection to the switch...\nDid the switch go to sleep?", "Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InventoryAutoRefreshToggle_CheckedChanged(object sender, EventArgs e)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath.Replace(".exe", ".dll"));

            if (this.InventoryAutoRefreshToggle.Checked)
            {
                InventoryRefreshTimer.Start();
                config.AppSettings.Settings["autoRefresh"].Value = "true";
            }
            else
            {
                InventoryRefreshTimer.Stop();
                config.AppSettings.Settings["autoRefresh"].Value = "false";
            }

            config.Save(ConfigurationSaveMode.Minimal);

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }
        #endregion

        #region Update Inventroy
        private bool UpdateInventory()
        {
            AllowInventoryUpdate = false;

            try
            {
                byte[] Bank01to20 = Utilities.GetInventoryBank(socket, usb, 1);
                if (Bank01to20 == null)
                {
                    return true;
                }
                byte[] Bank21to40 = Utilities.GetInventoryBank(socket, usb, 21);
                if (Bank21to40 == null)
                {
                    return true;
                }
                //string Bank1 = Utilities.ByteToHexString(Bank01to20);
                //string Bank2 = Utilities.ByteToHexString(Bank21to40);

                //Debug.Print(Bank1);
                //Debug.Print(Bank2);

                foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                {
                    if (btn.Tag == null)
                        continue;

                    if (btn.Tag.ToString() == "")
                        continue;

                    int slotId = int.Parse(btn.Tag.ToString());

                    byte[] slotBytes = new byte[2];
                    byte[] flag1Bytes = new byte[1];
                    byte[] flag2Bytes = new byte[1];
                    byte[] dataBytes = new byte[4];
                    byte[] recipeBytes = new byte[2];
                    byte[] fenceBytes = new byte[2];

                    int slotOffset;
                    int countOffset;
                    int flag1Offset;
                    int flag2Offset;
                    if (slotId < 21)
                    {
                        slotOffset = ((slotId - 1) * 0x8);
                        flag1Offset = 0x3 + ((slotId - 1) * 0x8);
                        flag2Offset = 0x2 + ((slotId - 1) * 0x8);
                        countOffset = 0x4 + ((slotId - 1) * 0x8);
                    }
                    else
                    {
                        slotOffset = ((slotId - 21) * 0x8);
                        flag1Offset = 0x3 + ((slotId - 21) * 0x8);
                        flag2Offset = 0x2 + ((slotId - 21) * 0x8);
                        countOffset = 0x4 + ((slotId - 21) * 0x8);
                    }

                    if (slotId < 21)
                    {
                        Buffer.BlockCopy(Bank01to20, slotOffset, slotBytes, 0x0, 0x2);
                        Buffer.BlockCopy(Bank01to20, flag1Offset, flag1Bytes, 0x0, 0x1);
                        Buffer.BlockCopy(Bank01to20, flag2Offset, flag2Bytes, 0x0, 0x1);
                        Buffer.BlockCopy(Bank01to20, countOffset, dataBytes, 0x0, 0x4);
                        Buffer.BlockCopy(Bank01to20, countOffset, recipeBytes, 0x0, 0x2);
                        Buffer.BlockCopy(Bank01to20, countOffset + 0x2, fenceBytes, 0x0, 0x2);
                    }
                    else
                    {
                        Buffer.BlockCopy(Bank21to40, slotOffset, slotBytes, 0x0, 0x2);
                        Buffer.BlockCopy(Bank21to40, flag1Offset, flag1Bytes, 0x0, 0x1);
                        Buffer.BlockCopy(Bank21to40, flag2Offset, flag2Bytes, 0x0, 0x1);
                        Buffer.BlockCopy(Bank21to40, countOffset, dataBytes, 0x0, 0x4);
                        Buffer.BlockCopy(Bank21to40, countOffset, recipeBytes, 0x0, 0x2);
                        Buffer.BlockCopy(Bank21to40, countOffset + 0x2, fenceBytes, 0x0, 0x2);
                    }

                    string itemID = Utilities.flip(Utilities.ByteToHexString(slotBytes));
                    string itemData = Utilities.flip(Utilities.ByteToHexString(dataBytes));
                    string recipeData = Utilities.flip(Utilities.ByteToHexString(recipeBytes));
                    string fenceData = Utilities.flip(Utilities.ByteToHexString(fenceBytes));
                    string flag1 = Utilities.ByteToHexString(flag1Bytes);
                    string flag2 = Utilities.ByteToHexString(flag2Bytes);
                    UInt16 IntId = Convert.ToUInt16(itemID, 16);

                    //Debug.Print("Slot : " + slotId.ToString() + " ID : " + itemID + " Data : " + itemData + " recipeData : " + recipeData + " Flag1 : " + flag1 + " Flag2 : " + flag2);

                    if (itemID == "FFFE") //Nothing
                    {
                        btn.setup("", 0xFFFE, 0x0, "", "00", "00");
                        continue;
                    }
                    else if (itemID == "16A2") //Recipe
                    {
                        btn.setup(GetNameFromID(recipeData, recipeSource), 0x16A2, Convert.ToUInt32("0x" + itemData, 16), GetImagePathFromID(recipeData, recipeSource), "", flag1, flag2);
                        continue;
                    }
                    else if (itemID == "1095") //Delivery
                    {
                        btn.setup(GetNameFromID(recipeData, itemSource), 0x1095, Convert.ToUInt32("0x" + itemData, 16), GetImagePathFromID(recipeData, itemSource, Convert.ToUInt32("0x" + itemData, 16)), "", flag1, flag2);
                        continue;
                    }
                    else if (itemID == "16A1") //Bottle Message
                    {
                        btn.setup(GetNameFromID(recipeData, recipeSource), 0x16A1, Convert.ToUInt32("0x" + itemData, 16), GetImagePathFromID(recipeData, recipeSource), "", flag1, flag2);
                        continue;
                    }
                    else if (itemID == "0A13") // Fossil
                    {
                        btn.setup(GetNameFromID(recipeData, itemSource), 0x0A13, Convert.ToUInt32("0x" + itemData, 16), GetImagePathFromID(recipeData, itemSource), "", flag1, flag2);
                        continue;
                    }
                    else if (itemID == "315A" || itemID == "1618" || itemID == "342F") // Wall-Mounted
                    {
                        btn.setup(GetNameFromID(itemID, itemSource), Convert.ToUInt16("0x" + itemID, 16), Convert.ToUInt32("0x" + itemData, 16), GetImagePathFromID(itemID, itemSource, Convert.ToUInt32("0x" + itemData, 16)), GetImagePathFromID(recipeData, itemSource, Convert.ToUInt32("0x" + Utilities.translateVariationValueBack(fenceData), 16)), flag1, flag2);
                        continue;
                    }
                    else if (ItemAttr.hasFenceWithVariation(IntId)) // Fence Variation
                    {
                        btn.setup(GetNameFromID(itemID, itemSource), Convert.ToUInt16("0x" + itemID, 16), Convert.ToUInt32("0x" + itemData, 16), GetImagePathFromID(itemID, itemSource, Convert.ToUInt32("0x" + fenceData, 16)), "", flag1, flag2);
                        continue;
                    }
                    else
                    {
                        btn.setup(GetNameFromID(itemID, itemSource), Convert.ToUInt16("0x" + itemID, 16), Convert.ToUInt32("0x" + itemData, 16), GetImagePathFromID(itemID, itemSource, Convert.ToUInt32("0x" + itemData, 16)), "", flag1, flag2);
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.logEvent("MainForm", "UpdateInventory: " + ex.Message.ToString());
                Invoke((MethodInvoker)delegate { this.InventoryAutoRefreshToggle.Checked = false; });
                InventoryRefreshTimer.Stop();
                MyMessageBox.Show(ex.Message.ToString(), "This seems like a bad idea but it's fine for now.");
                return true;
            }

            AllowInventoryUpdate = true;

            return false;
        }
        #endregion

        private void deletedSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int index = FavGridView.CurrentCell.RowIndex;
            if (FavGridView.CurrentCell != null)
            {
                //Debug.Print(index.ToString());

                string id = FavGridView.Rows[index].Cells["id"].Value.ToString();
                string iName = FavGridView.Rows[index].Cells["iName"].Value.ToString();
                string value = FavGridView.Rows[index].Cells["value"].Value.ToString();

                FileDeleteLine(Utilities.favPath, id, iName, value);

                FavGridView.Rows.RemoveAt(index);
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
        }

        private void FileDeleteLine(string Path, string key1, string key2, string key3)
        {
            StringBuilder sb = new StringBuilder();
            string line;
            using (StreamReader sr = new StreamReader(Path))
            {
                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine();
                    if (!line.Contains(key1) || !line.Contains(key2) || !line.Contains(key3))
                    {
                        using (StringWriter sw = new StringWriter(sb))
                        {
                            sw.WriteLine(line);
                        }
                    }
                    else
                    {
                        //MessageBox.Show(line);
                    }
                }
            }
            using (StreamWriter sw = new StreamWriter(Path))
            {
                sw.Write(sb.ToString());
            }
        }

        private void Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            MyLog.logEvent("MainForm", "Form Closed");
        }

        private void openVariationMenu()
        {
            selection = new variation();
            selection.sendVariationData += Selection_sendVariationData;
            selection.Show();
            selection.Location = new System.Drawing.Point(this.Location.X + 7, this.Location.Y + this.Height);
            string id = Utilities.precedingZeros(SelectedItem.fillItemID(), 4);
            string value = Utilities.precedingZeros(SelectedItem.fillItemData(), 8);
            UInt16 IntId = Convert.ToUInt16(id, 16);
            if (ItemAttr.hasFenceWithVariation(IntId))  // Fence Variation
            {
                selection.receiveID(Utilities.precedingZeros(SelectedItem.fillItemID(), 4), languageSetting, value);
            }
            else if (id == "315A" || id == "1618" || id == "342F")
            {
                selection.receiveID(Utilities.turn2bytes(SelectedItem.fillItemData()), languageSetting);
            }
            else
            {
                selection.receiveID(Utilities.precedingZeros(SelectedItem.fillItemID(), 4), languageSetting);
            }
            VariationButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
        }

        private void Selection_sendVariationData(inventorySlot item, int type)
        {
            if (type == 0) //Left click
            {
                SelectedItem.setup(item);
                if (HexModeButton.Tag.ToString() == "Normal")
                {
                    HexModeButton_Click(null, null);
                }
                updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
                AmountOrCountTextbox.Text = Utilities.precedingZeros(SelectedItem.fillItemData(), 8);
                IDTextbox.Text = Utilities.precedingZeros(SelectedItem.fillItemID(), 4);
            }
            else if (type == 1) // Right click
            {
                if (IDTextbox.Text == "315A" || IDTextbox.Text == "1618" || IDTextbox.Text == "342F")
                {
                    if (HexModeButton.Tag.ToString() == "Normal")
                    {
                        HexModeButton_Click(null, null);
                    }

                    string count = Utilities.translateVariationValue(item.fillItemData()) + Utilities.precedingZeros(item.fillItemID(), 4);

                    SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + count, 16), GetImagePathFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), true, item.getPath(), SelectedItem.getFlag1(), SelectedItem.getFlag2());
                    AmountOrCountTextbox.Text = count;
                }
            }
        }

        private void closeVariationMenu()
        {
            if (selection != null)
            {
                selection.Dispose();
                selection = null;
                VariationButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            }
        }

        private void Main_LocationChanged(object sender, EventArgs e)
        {
            if (selection != null)
            {
                selection.Location = new System.Drawing.Point(this.Location.X + 7, this.Location.Y + this.Height);
            }
        }

        private void SelectedItem_Click(object sender, EventArgs e)
        {
            if (selectedButton == null)
            {
                int Slot = findEmpty();
                if (Slot > 0)
                {
                    selectedSlot = Slot;
                    updateSlot(Slot);
                }
            }

            if (currentPanel == ItemModePanel)
            {
                NormalItemSpawn();
            }
            else if (currentPanel == RecipeModePanel)
            {
                RecipeSpawn();
            }
            else if (currentPanel == FlowerModePanel)
            {
                FlowerSpawn();
            }
            int firstSlot = findEmpty();
            if (firstSlot > 0)
            {
                selectedSlot = firstSlot;
                updateSlot(firstSlot);
            }
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void updateSlot(int select)
        {
            foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
            {
                if (btn.Tag == null)
                    continue;
                btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
                if (int.Parse(btn.Tag.ToString()) == select)
                {
                    selectedButton = btn;
                    btn.BackColor = System.Drawing.Color.LightSeaGreen;
                }
            }
        }

        private int findEmpty()
        {
            inventorySlot[] SlotPointer = new inventorySlot[40];

            foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
            {
                int slotId = int.Parse(btn.Tag.ToString());
                SlotPointer[slotId - 1] = btn;
            }
            for (int i = 0; i < SlotPointer.Length; i++)
            {
                if (SlotPointer[i].fillItemID() == "FFFE")
                    return i + 1;
            }

            return -1;
        }

        private void NormalItemSpawn()
        {
            if (IDTextbox.Text == "")
            {
                MessageBox.Show("Please enter an ID before sending item");
                return;
            }

            if (AmountOrCountTextbox.Text == "")
            {
                MessageBox.Show("Please enter an amount");
                return;
            }

            if (selectedButton == null)
            {
                MessageBox.Show("Please select a slot");
                return;
            }

            string hexValue = "00000000";
            if (HexModeButton.Tag.ToString() == "Normal")
            {
                int decValue = Convert.ToInt32(AmountOrCountTextbox.Text) - 1;
                if (decValue >= 0)
                    hexValue = Utilities.precedingZeros(decValue.ToString("X"), 8);
            }
            else
                hexValue = Utilities.precedingZeros(AmountOrCountTextbox.Text, 8);

            UInt16 IntId = Convert.ToUInt16(IDTextbox.Text, 16);

            string front = Utilities.precedingZeros(hexValue, 8).Substring(0, 4);
            string back = Utilities.precedingZeros(hexValue, 8).Substring(4, 4);

            try
            {
                if (IDTextbox.Text == "16A2") //recipe
                {
                    if (!offline)
                        Utilities.SpawnItem(socket, usb, selectedSlot, SelectedItem.getFlag1() + SelectedItem.getFlag2() + IDTextbox.Text, Utilities.precedingZeros(hexValue, 8));
                    selectedButton.setup(GetNameFromID(Utilities.turn2bytes(hexValue), recipeSource), 0x16A2, Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(hexValue), recipeSource));
                }
                else if (IDTextbox.Text == "315A" || IDTextbox.Text == "1618" || IDTextbox.Text == "342F") // Wall-Mounted
                {
                    if (!offline)
                        Utilities.SpawnItem(socket, usb, selectedSlot, SelectedItem.getFlag1() + SelectedItem.getFlag2() + IDTextbox.Text, Utilities.precedingZeros(hexValue, 8));
                    selectedButton.setup(GetNameFromID(IDTextbox.Text, itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(IDTextbox.Text, itemSource, Convert.ToUInt32("0x" + hexValue, 16)), GetImagePathFromID((Utilities.turn2bytes(hexValue)), itemSource, Convert.ToUInt32("0x" + Utilities.translateVariationValueBack(front), 16)), SelectedItem.getFlag1(), SelectedItem.getFlag2());
                }
                else if (ItemAttr.hasFenceWithVariation(IntId))  // Fence Variation
                {
                    if (!offline)
                        Utilities.SpawnItem(socket, usb, selectedSlot, SelectedItem.getFlag1() + SelectedItem.getFlag2() + IDTextbox.Text, Utilities.precedingZeros(hexValue, 8));
                    selectedButton.setup(GetNameFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource, Convert.ToUInt32("0x" + front, 16)), "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
                }
                else
                {
                    if (!offline)
                        Utilities.SpawnItem(socket, usb, selectedSlot, SelectedItem.getFlag1() + SelectedItem.getFlag2() + IDTextbox.Text, Utilities.precedingZeros(hexValue, 8));
                    selectedButton.setup(GetNameFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource, Convert.ToUInt32("0x" + hexValue, 16)), "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
                }
            }
            catch (Exception ex)
            {
                MyLog.logEvent("MainForm", "SpawnItem: " + ex.Message.ToString());
                MyMessageBox.Show(ex.Message.ToString(), "FIXME: This doesn't account for children of hierarchy... too bad!");
            }

            //this.ShowMessage(IDTextbox.Text);
        }

        private void RecipeSpawn()
        {
            if (RecipeIDTextbox.Text == "")
            {
                MessageBox.Show("Please enter a recipe ID before sending item");
                return;
            }

            if (selectedButton == null)
            {
                MessageBox.Show("Please select a slot");
                return;
            }

            if (!offline)
                Utilities.SpawnRecipe(socket, usb, selectedSlot, "16A2", Utilities.turn2bytes(RecipeIDTextbox.Text));

            //this.ShowMessage(Utilities.turn2bytes(RecipeIDTextbox.Text));

            selectedButton.setup(GetNameFromID(Utilities.turn2bytes(RecipeIDTextbox.Text), recipeSource), 0x16A2, Convert.ToUInt32("0x" + RecipeIDTextbox.Text, 16), GetImagePathFromID(Utilities.turn2bytes(RecipeIDTextbox.Text), recipeSource));
        }

        private void FlowerSpawn()
        {
            if (FlowerIDTextbox.Text == "")
            {
                MessageBox.Show("Please select a flower");
                return;
            }

            if (selectedButton == null)
            {
                MessageBox.Show("Please select a slot");
                return;
            }

            if (!offline)
                Utilities.SpawnFlower(socket, usb, selectedSlot, FlowerIDTextbox.Text, FlowerValueTextbox.Text);

            //this.ShowMessage(FlowerIDTextbox.Text);

            selectedButton.setup(GetNameFromID(FlowerIDTextbox.Text, itemSource), Convert.ToUInt16("0x" + FlowerIDTextbox.Text, 16), Convert.ToUInt32("0x" + FlowerValueTextbox.Text, 16), GetImagePathFromID(FlowerIDTextbox.Text, itemSource));

        }

        private void DeleteItem()
        {
            if (selectedButton == null)
            {
                MessageBox.Show("Please select a slot");
                return;
            }

            if (!offline)
            {
                try
                {
                    Utilities.DeleteSlot(socket, usb, int.Parse(selectedButton.Tag.ToString()));
                }
                catch (Exception ex)
                {
                    MyLog.logEvent("MainForm", "DeleteItemKeyBoard: " + ex.Message.ToString());
                    MyMessageBox.Show(ex.Message.ToString(), "Because nobody could *ever* possible attempt to parse bad data.");
                }
            }
            selectedButton.reset();
            ButtonToolTip.RemoveAll();

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void CopyItem(object sender, KeyEventArgs e)
        {
            ItemModeButton_Click(sender, e);
            if (HexModeButton.Tag.ToString() == "Normal")
            {
                HexModeButton_Click(sender, e);
            }

            string hexValue = "0";
            int decValue = Convert.ToInt32("0x" + AmountOrCountTextbox.Text, 16) - 1;
            if (decValue >= 0)
                hexValue = decValue.ToString("X");

            SelectedItem.setup(selectedButton);
            if (selection != null)
            {
                selection.receiveID(Utilities.precedingZeros(SelectedItem.fillItemID(), 4), languageSetting, Utilities.precedingZeros(hexValue, 8));
            }
            updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
            AmountOrCountTextbox.Text = Utilities.precedingZeros(SelectedItem.fillItemData(), 8);
            IDTextbox.Text = Utilities.precedingZeros(SelectedItem.fillItemID(), 4);
        }

        private void InventorySlot_Click(object sender, EventArgs e)
        {
            var button = (inventorySlot)sender;

            foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
            {
                if (btn.Tag == null)
                    continue;
                btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            }

            button.BackColor = System.Drawing.Color.LightSeaGreen;
            selectedButton = button;
            selectedSlot = int.Parse(button.Tag.ToString());

            if (Control.ModifierKeys == Keys.Control)
            {
                if (currentPanel == ItemModePanel)
                {
                    NormalItemSpawn();
                }
                else if (currentPanel == RecipeModePanel)
                {
                    RecipeSpawn();
                }
                else if (currentPanel == FlowerModePanel)
                {
                    FlowerSpawn();
                }

                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
            else if (Control.ModifierKeys == Keys.Alt)
            {
                DeleteItem();
            }
        }

        private void copyItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripItem item = (sender as ToolStripItem);
            if (item != null)
            {
                if (item.Owner is ContextMenuStrip owner)
                {
                    ItemModeButton_Click(sender, e);
                    if (HexModeButton.Tag.ToString() == "Normal")
                    {
                        HexModeButton_Click(sender, e);
                    }
                    var btn = (inventorySlot)owner.SourceControl;

                    SelectedItem.setup(btn);

                    if (SelectedItem.fillItemID() == "FFFE")
                    {
                        HexModeButton_Click(sender, e);
                        AmountOrCountTextbox.Text = "";
                        IDTextbox.Text = "";
                    }
                    else
                    {
                        AmountOrCountTextbox.Text = Utilities.precedingZeros(SelectedItem.fillItemData(), 8);
                        IDTextbox.Text = Utilities.precedingZeros(SelectedItem.fillItemID(), 4);
                    }

                    string hexValue = Utilities.precedingZeros(IDTextbox.Text, 8);

                    if (selection != null)
                    {
                        selection.receiveID(Utilities.precedingZeros(SelectedItem.fillItemID(), 4), languageSetting, Utilities.precedingZeros(hexValue, 8));
                    }
                    updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
                    if (sound)
                        System.Media.SystemSounds.Asterisk.Play();
                }
            }
        }

        private void deleteItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripItem item = (sender as ToolStripItem);
            if (item != null)
            {
                if (item.Owner is ContextMenuStrip owner)
                {
                    if (!offline)
                    {
                        int slotId = int.Parse(owner.SourceControl.Tag.ToString());
                        try
                        {
                            Utilities.DeleteSlot(socket, usb, slotId);
                        }
                        catch (Exception ex)
                        {
                            MyLog.logEvent("MainForm", "DeleteItemRightClick: " + ex.Message.ToString());
                            MyMessageBox.Show(ex.Message.ToString(), "Bizarre vector flip inherited from earlier code, WTF?");
                        }
                    }

                    var btnParent = (inventorySlot)owner.SourceControl;
                    btnParent.reset();
                    ButtonToolTip.RemoveAll();
                    if (sound)
                        System.Media.SystemSounds.Asterisk.Play();
                }
            }
        }

        private void wrapItemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (WrapSelector.SelectedIndex < 0)
                WrapSelector.SelectedIndex = 0;

            string[] flagBuffer = WrapSelector.SelectedItem.ToString().Split(' ');
            byte flagByte = Utilities.stringToByte(flagBuffer[flagBuffer.Length - 1])[0];

            ToolStripItem item = (sender as ToolStripItem);
            if (item != null)
            {
                if (item.Owner is ContextMenuStrip owner)
                {
                    string flag = "00";
                    if (RetainNameToggle.Checked)
                    {
                        flag = Utilities.precedingZeros((flagByte + 0x40).ToString("X"), 2);
                    }
                    else
                    {
                        flag = Utilities.precedingZeros((flagByte).ToString("X"), 2);
                    }

                    if (!offline)
                    {
                        byte[] Bank01to20 = Utilities.GetInventoryBank(socket, usb, 1);
                        byte[] Bank21to40 = Utilities.GetInventoryBank(socket, usb, 21);

                        int slot = int.Parse(owner.SourceControl.Tag.ToString());
                        byte[] slotBytes = new byte[2];

                        int slotOffset;
                        if (slot < 21)
                        {
                            slotOffset = ((slot - 1) * 0x8);
                        }
                        else
                        {
                            slotOffset = ((slot - 21) * 0x8);
                        }

                        if (slot < 21)
                        {
                            Buffer.BlockCopy(Bank01to20, slotOffset, slotBytes, 0x0, 0x2);
                        }
                        else
                        {
                            Buffer.BlockCopy(Bank21to40, slotOffset, slotBytes, 0x0, 0x2);
                        }

                        string slotID = Utilities.flip(Utilities.ByteToHexString(slotBytes));

                        if (slotID == "FFFE")
                        {
                            return;
                        }

                        Utilities.setFlag1(socket, usb, slot, flag);

                        var btnParent = (inventorySlot)owner.SourceControl;
                        btnParent.setFlag1(flag);
                        btnParent.refresh(false);
                    }
                    else
                    {
                        var btnParent = (inventorySlot)owner.SourceControl;
                        if (btnParent.fillItemID() != "FFFE")
                        {
                            btnParent.setFlag1(flag);
                            btnParent.refresh(false);
                        }
                    }
                    if (sound)
                        System.Media.SystemSounds.Asterisk.Play();
                }
            }
        }

        private void wrapAllItemsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (WrapSelector.SelectedIndex < 0)
                WrapSelector.SelectedIndex = 0;

            string[] flagBuffer = WrapSelector.SelectedItem.ToString().Split(' ');
            byte flagByte = Utilities.stringToByte(flagBuffer[flagBuffer.Length - 1])[0];
            Thread wrapAllThread = new Thread(delegate () { wrapAll(flagByte); });
            wrapAllThread.Start();
        }

        private void wrapAll(byte flagByte)
        {
            showWait();

            string flag = "00";
            if (RetainNameToggle.Checked)
            {
                flag = Utilities.precedingZeros((flagByte + 0x40).ToString("X"), 2);
            }
            else
            {
                flag = Utilities.precedingZeros((flagByte).ToString("X"), 2);
            }

            if (!offline)
            {
                byte[] Bank01to20 = Utilities.GetInventoryBank(socket, usb, 1);
                byte[] Bank21to40 = Utilities.GetInventoryBank(socket, usb, 21);

                foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                {
                    int slot = int.Parse(btn.Tag.ToString());
                    byte[] slotBytes = new byte[2];

                    int slotOffset;
                    if (slot < 21)
                    {
                        slotOffset = ((slot - 1) * 0x8);
                    }
                    else
                    {
                        slotOffset = ((slot - 21) * 0x8);
                    }

                    if (slot < 21)
                    {
                        Buffer.BlockCopy(Bank01to20, slotOffset, slotBytes, 0x0, 0x2);
                    }
                    else
                    {
                        Buffer.BlockCopy(Bank21to40, slotOffset, slotBytes, 0x0, 0x2);
                    }

                    string slotID = Utilities.flip(Utilities.ByteToHexString(slotBytes));

                    if (slotID != "FFFE")
                    {
                        Utilities.setFlag1(socket, usb, slot, flag);
                        Invoke((MethodInvoker)delegate
                        {
                            btn.setFlag1(flag);
                            btn.refresh(false);
                        });
                    }
                }
            }
            else
            {
                foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                {
                    if (btn.fillItemID() != "FFFE")
                    {
                        Invoke((MethodInvoker)delegate
                        {
                            btn.setFlag1(flag);
                            btn.refresh(false);
                        });
                    }
                }
            }
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();

            hideWait();
        }

        private void addToFavoriteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripItem item = (sender as ToolStripItem);
            if (item != null)
            {
                if (item.Owner is ContextMenuStrip owner)
                {
                    var btn = (inventorySlot)owner.SourceControl;
                    if (btn.fillItemID() == "FFFE")
                    {
                        return;
                    }
                    else
                    {
                        DataTable dt = (DataTable)FavGridView.DataSource;
                        DataRow dr = dt.NewRow();
                        dr["id"] = Utilities.turn2bytes(btn.fillItemID());
                        dr["iName"] = btn.getiName();
                        dr["Name"] = btn.displayItemName();
                        dr["value"] = Utilities.precedingZeros(btn.fillItemData(), 8);

                        dt.Rows.Add(dr);
                        FavGridView.DataSource = dt;

                        string line = dr["id"] + " ; " + dr["iName"] + " ; " + dr["Name"] + " ; " + dr["value"] + " ; ";

                        if (!File.Exists(Utilities.favPath))
                        {
                            string favheader = "id" + " ; " + "iName" + " ; " + "Name" + " ; " + "value" + " ; ";

                            using (StreamWriter sw = File.CreateText(Utilities.favPath))
                            {
                                sw.WriteLine(favheader);
                            }
                        }

                        using (StreamWriter sw = File.AppendText(Utilities.favPath))
                        {
                            sw.WriteLine(line);
                        }

                    }
                    if (sound)
                        System.Media.SystemSounds.Asterisk.Play();
                }
            }
        }

        private void unwrapAllItemsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Thread unwrapAllThread = new Thread(delegate () { unwrapAll(); });
            unwrapAllThread.Start();
        }

        private void unwrapAll()
        {
            showWait();

            if (!offline)
            {
                byte[] Bank01to20 = Utilities.GetInventoryBank(socket, usb, 1);
                byte[] Bank21to40 = Utilities.GetInventoryBank(socket, usb, 21);

                foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                {
                    int slot = int.Parse(btn.Tag.ToString());
                    byte[] slotBytes = new byte[2];

                    int slotOffset;
                    if (slot < 21)
                    {
                        slotOffset = ((slot - 1) * 0x8);
                    }
                    else
                    {
                        slotOffset = ((slot - 21) * 0x8);
                    }

                    if (slot < 21)
                    {
                        Buffer.BlockCopy(Bank01to20, slotOffset, slotBytes, 0x0, 0x2);
                    }
                    else
                    {
                        Buffer.BlockCopy(Bank21to40, slotOffset, slotBytes, 0x0, 0x2);
                    }

                    string slotID = Utilities.flip(Utilities.ByteToHexString(slotBytes));

                    if (slotID != "FFFE")
                    {
                        Utilities.setFlag1(socket, usb, slot, "00");
                        Invoke((MethodInvoker)delegate
                        {
                            btn.setFlag1("00");
                            btn.refresh(false);
                        });
                    }
                }
            }
            else
            {
                foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                {
                    if (btn.fillItemID() != "FFFE")
                    {
                        Invoke((MethodInvoker)delegate
                        {
                            btn.setFlag1("00");
                            btn.refresh(false);
                        });
                    }
                }
            }
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();

            hideWait();
        }

        private void ItemModeButton_Click(object sender, EventArgs e)
        {
            this.ItemModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            this.RecipeModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.FlowerModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.FavoriteModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            this.RecipeModePanel.Visible = false;
            this.ItemModePanel.Visible = true;
            this.FlowerModePanel.Visible = false;

            this.ItemGridView.Visible = true;
            this.RecipeGridView.Visible = false;
            this.FlowerGridView.Visible = false;
            this.FavGridView.Visible = false;

            this.VariationButton.Visible = true;


            string hexValue = "00000000";
            if (HexModeButton.Tag.ToString() == "Normal")
            {
                if (AmountOrCountTextbox.Text == "")
                    AmountOrCountTextbox.Text = "1";
                int decValue = Convert.ToInt32(AmountOrCountTextbox.Text) - 1;
                if (decValue >= 0)
                    hexValue = Utilities.precedingZeros(decValue.ToString("X"), 8);
            }
            else
            {
                if (AmountOrCountTextbox.Text == "")
                    AmountOrCountTextbox.Text = "00000000";
                hexValue = Utilities.precedingZeros(AmountOrCountTextbox.Text, 8);
            }

            currentPanel = ItemModePanel;

            if (IDTextbox.Text == "")
                return;

            UInt16 IntId = Convert.ToUInt16("0x" + IDTextbox.Text, 16);
            string front = hexValue.Substring(0, 4);
            string back = hexValue.Substring(4, 4);

            if (IDTextbox.Text != "")
            {
                if (IDTextbox.Text == "16A2") //recipe
                {
                    SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(hexValue), recipeSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(hexValue), recipeSource), true, "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
                }
                else if (IDTextbox.Text == "315A" || IDTextbox.Text == "1618" || IDTextbox.Text == "342F") // Wall-Mounted
                {
                    SelectedItem.setup(GetNameFromID(IDTextbox.Text, itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(IDTextbox.Text, itemSource, Convert.ToUInt32("0x" + hexValue, 16)), true, GetImagePathFromID((Utilities.turn2bytes(hexValue)), itemSource, Convert.ToUInt32("0x" + Utilities.translateVariationValueBack(front), 16)), SelectedItem.getFlag1(), SelectedItem.getFlag2());
                }
                else if (ItemAttr.hasFenceWithVariation(IntId)) // Fence Variation
                {
                    SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource, Convert.ToUInt32("0x" + front, 16)), true, "", SelectedItem.getFlag1(), SelectedItem.getFlag2());

                    if (selection != null)
                    {
                        selection.receiveID(IDTextbox.Text, languageSetting, Utilities.precedingZeros(hexValue, 8));
                    }
                }
                else
                {
                    SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource, Convert.ToUInt32("0x" + hexValue, 16)), true, "", SelectedItem.getFlag1(), SelectedItem.getFlag2());

                    if (selection != null)
                    {
                        selection.receiveID(IDTextbox.Text, languageSetting, Utilities.precedingZeros(hexValue, 8));
                    }
                }
                updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
            }
            else
            {
                SelectedItem.reset();
                SelectedItemName.Text = "";
            }

            if (ItemSearchBox.Text != "Search")
            {
                ItemSearchBox.Clear();
            }
        }

        private void RecipeModeButton_Click(object sender, EventArgs e)
        {
            this.ItemModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.RecipeModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            this.FlowerModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.FavoriteModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            this.ItemModePanel.Visible = false;
            this.RecipeModePanel.Visible = true;
            this.FlowerModePanel.Visible = false;

            this.ItemGridView.Visible = false;
            this.RecipeGridView.Visible = true;
            this.FlowerGridView.Visible = false;
            this.FavGridView.Visible = false;

            this.VariationButton.Visible = false;
            closeVariationMenu();

            if (RecipeIDTextbox.Text != "")
            {
                //Debug.Print(GetNameFromID(RecipeIDTextbox.Text, recipeSource));
                SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(RecipeIDTextbox.Text), recipeSource), 0x16A2, Convert.ToUInt32("0x" + RecipeIDTextbox.Text, 16), GetImagePathFromID(Utilities.turn2bytes(RecipeIDTextbox.Text), recipeSource), true);
                updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
            }
            else
            {
                SelectedItem.reset();
                SelectedItemName.Text = "";
            }

            currentPanel = RecipeModePanel;

            if (ItemSearchBox.Text != "Search")
            {
                ItemSearchBox.Clear();
            }
        }

        private void FlowerModeButton_Click(object sender, EventArgs e)
        {
            this.ItemModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.RecipeModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.FlowerModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            this.FavoriteModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            this.ItemModePanel.Visible = false;
            this.RecipeModePanel.Visible = false;
            this.FlowerModePanel.Visible = true;

            this.ItemGridView.Visible = false;
            this.RecipeGridView.Visible = false;
            this.FlowerGridView.Visible = true;
            this.FavGridView.Visible = false;

            this.VariationButton.Visible = false;
            closeVariationMenu();

            if (FlowerIDTextbox.Text != "")
            {
                SelectedItem.setup(GetNameFromID(FlowerIDTextbox.Text, itemSource), Convert.ToUInt16("0x" + FlowerIDTextbox.Text, 16), Convert.ToUInt32("0x" + FlowerValueTextbox.Text, 16), GetImagePathFromID(FlowerIDTextbox.Text, itemSource), true);
                updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
            }
            else
            {
                SelectedItem.reset();
                SelectedItemName.Text = "";
            }

            currentPanel = FlowerModePanel;

            if (ItemSearchBox.Text != "Search")
            {
                ItemSearchBox.Clear();
            }
        }

        private void FavoriteModeButton_Click(object sender, EventArgs e)
        {
            this.ItemModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.RecipeModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.FlowerModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.FavoriteModeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));

            this.RecipeModePanel.Visible = false;
            this.ItemModePanel.Visible = true;
            this.FlowerModePanel.Visible = false;

            this.ItemGridView.Visible = false;
            this.RecipeGridView.Visible = false;
            this.FlowerGridView.Visible = false;
            this.FavGridView.Visible = true;

            this.VariationButton.Visible = true;

            currentPanel = ItemModePanel;

            string hexValue = "00000000";
            if (HexModeButton.Tag.ToString() == "Normal")
            {
                if (AmountOrCountTextbox.Text == "")
                    AmountOrCountTextbox.Text = "1";
                int decValue = Convert.ToInt32(AmountOrCountTextbox.Text) - 1;
                if (decValue >= 0)
                    hexValue = Utilities.precedingZeros(decValue.ToString("X"), 8);
            }
            else
            {
                if (AmountOrCountTextbox.Text == "")
                    AmountOrCountTextbox.Text = "00000000";
                hexValue = Utilities.precedingZeros(AmountOrCountTextbox.Text, 8);
            }

            if (IDTextbox.Text == "")
                return;

            UInt16 IntId = Convert.ToUInt16("0x" + IDTextbox.Text, 16);
            string front = hexValue.Substring(0, 4);
            string back = hexValue.Substring(4, 4);

            if (IDTextbox.Text != "")
            {
                if (IDTextbox.Text == "16A2") //recipe
                {
                    SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(hexValue), recipeSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(hexValue), recipeSource), true, "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
                }
                else if (IDTextbox.Text == "315A" || IDTextbox.Text == "1618" || IDTextbox.Text == "342F") // Wall-Mounted
                {
                    SelectedItem.setup(GetNameFromID(IDTextbox.Text, itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(IDTextbox.Text, itemSource, Convert.ToUInt32("0x" + hexValue, 16)), true, GetImagePathFromID((Utilities.turn2bytes(hexValue)), itemSource, Convert.ToUInt32("0x" + Utilities.translateVariationValueBack(front), 16)), SelectedItem.getFlag1(), SelectedItem.getFlag2());
                }
                else if (ItemAttr.hasFenceWithVariation(IntId)) // Fence Variation
                {
                    SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource, Convert.ToUInt32("0x" + front, 16)), true, "", SelectedItem.getFlag1(), SelectedItem.getFlag2());

                    if (selection != null)
                    {
                        selection.receiveID(IDTextbox.Text, languageSetting, Utilities.precedingZeros(hexValue, 8));
                    }
                }
                else
                {
                    SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource, Convert.ToUInt32("0x" + hexValue, 16)), true, "", SelectedItem.getFlag1(), SelectedItem.getFlag2());

                    if (selection != null)
                    {
                        selection.receiveID(IDTextbox.Text, languageSetting, Utilities.precedingZeros(hexValue, 8));
                    }
                }
                updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
            }
            else
            {
                SelectedItem.reset();
                SelectedItemName.Text = "";
            }

            if (ItemSearchBox.Text != "Search")
            {
                ItemSearchBox.Clear();
            }
        }

        private void VariationButton_Click(object sender, EventArgs e)
        {
            if (selection == null)
            {
                openVariationMenu();
            }
            else
            {
                closeVariationMenu();
            }
        }

        private void InventorySlot_MouseHover(object sender, EventArgs e)
        {
            var button = (inventorySlot)sender;
            if (!button.isEmpty())
            {
                ButtonToolTip.SetToolTip(button, button.displayItemName() + "\n\nID : " + button.displayItemID() + "\nCount : " + button.displayItemData() + "\nFlag : 0x" + button.getFlag1() + button.getFlag2());
            }
        }

        #region Progessbar
        private void showWait()
        {
            if (InvokeRequired)
            {
                MethodInvoker method = new MethodInvoker(showWait);
                Invoke(method);
                return;
            }
            LoadingPanel.Visible = true;

            ItemModePanel.Visible = false;
            RecipeModePanel.Visible = false;
            FlowerModePanel.Visible = false;

            ItemModeButton.Visible = false;
            RecipeModeButton.Visible = false;
            FlowerModeButton.Visible = false;

            SelectedItem.Visible = false;
            SelectedItemName.Visible = false;
        }

        private void hideWait()
        {
            if (InvokeRequired)
            {
                MethodInvoker method = new MethodInvoker(hideWait);
                Invoke(method);
                return;
            }
            LoadingPanel.Visible = false;
            currentPanel.Visible = true;

            ItemModeButton.Visible = true;
            RecipeModeButton.Visible = true;
            FlowerModeButton.Visible = true;

            SelectedItem.Visible = true;
            SelectedItemName.Visible = true;
        }
        #endregion

        private void SaveNHIButton_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog file = new SaveFileDialog()
                {
                    Filter = "New Horizons Inventory (*.nhi)|*.nhi",
                    //FileName = "items.nhi",
                };

                Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath.Replace(".exe", ".dll"));

                string savepath;

                if (config.AppSettings.Settings["LastSave"].Value.Equals(string.Empty))
                    savepath = Directory.GetCurrentDirectory() + @"\save";
                else
                    savepath = config.AppSettings.Settings["LastSave"].Value;

                //Debug.Print(savepath);
                if (Directory.Exists(savepath))
                {
                    file.InitialDirectory = savepath;
                }
                else
                {
                    file.InitialDirectory = @"C:\";
                }

                if (file.ShowDialog() != DialogResult.OK)
                    return;

                string[] temp = file.FileName.Split('\\');
                string path = "";
                for (int i = 0; i < temp.Length - 1; i++)
                    path = path + temp[i] + "\\";

                config.AppSettings.Settings["LastSave"].Value = path;
                config.Save(ConfigurationSaveMode.Minimal);


                string Bank = "";

                if (!offline)
                {
                    byte[] Bank01to20 = Utilities.GetInventoryBank(socket, usb, 1);
                    byte[] Bank21to40 = Utilities.GetInventoryBank(socket, usb, 21);
                    Bank = Utilities.ByteToHexString(Bank01to20) + Utilities.ByteToHexString(Bank21to40);


                    byte[] save = new byte[320];

                    Array.Copy(Bank01to20, 0, save, 0, 160);
                    Array.Copy(Bank21to40, 0, save, 160, 160);

                    File.WriteAllBytes(file.FileName, save);
                }
                else
                {
                    inventorySlot[] SlotPointer = new inventorySlot[40];
                    foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                    {
                        int slotId = int.Parse(btn.Tag.ToString());
                        SlotPointer[slotId - 1] = btn;
                    }
                    for (int i = 0; i < SlotPointer.Length; i++)
                    {
                        string first = Utilities.flip(Utilities.precedingZeros(SlotPointer[i].getFlag1() + SlotPointer[i].getFlag2() + Utilities.precedingZeros(SlotPointer[i].fillItemID(), 4), 8));
                        string second = Utilities.flip(Utilities.precedingZeros(SlotPointer[i].fillItemData(), 8));
                        //Debug.Print(first + " " + second + " " + SlotPointer[i].getFlag1() + " " + SlotPointer[i].getFlag2() + " " + SlotPointer[i].fillItemID());
                        Bank = Bank + first + second;
                    }

                    byte[] save = new byte[320];

                    for (int i = 0; i < Bank.Length / 2 - 1; i++)
                    {
                        string data = String.Concat(Bank[(i * 2)].ToString(), Bank[((i * 2) + 1)].ToString());
                        //Debug.Print(i.ToString() + " " + data);
                        save[i] = Convert.ToByte(data, 16);
                    }

                    File.WriteAllBytes(file.FileName, save);
                }
                //Debug.Print(Bank);
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
            catch
            {
                return;
            }
        }

        private void LoadNHIButton_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog file = new OpenFileDialog()
                {
                    Filter = "New Horizons Inventory (*.nhi)|*.nhi|All files (*.*)|*.*",
                    FileName = "items.nhi",
                };

                Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath.Replace(".exe", ".dll"));

                string savepath;

                if (config.AppSettings.Settings["LastLoad"].Value.Equals(string.Empty))
                    savepath = Directory.GetCurrentDirectory() + @"\save";
                else
                    savepath = config.AppSettings.Settings["LastLoad"].Value;

                if (Directory.Exists(savepath))
                {
                    file.InitialDirectory = savepath;
                }
                else
                {
                    file.InitialDirectory = @"C:\";
                }

                if (file.ShowDialog() != DialogResult.OK)
                    return;

                string[] temp = file.FileName.Split('\\');
                string path = "";
                for (int i = 0; i < temp.Length - 1; i++)
                    path = path + temp[i] + "\\";

                config.AppSettings.Settings["LastLoad"].Value = path;
                config.Save(ConfigurationSaveMode.Minimal);

                byte[] data = File.ReadAllBytes(file.FileName);

                ButtonToolTip.RemoveAll();

                Thread LoadThread = new Thread(delegate () { loadInventory(data); });
                LoadThread.Start();
            }
            catch
            {
                return;
            }
        }

        private void loadInventory(byte[] data)
        {
            showWait();

            byte[][] item = processNHI(data);

            string Bank = "";

            byte[] b1 = new byte[160];
            byte[] b2 = new byte[160];

            if (!offline)
            {
                byte[] Bank01to20 = Utilities.GetInventoryBank(socket, usb, 1);
                byte[] Bank21to40 = Utilities.GetInventoryBank(socket, usb, 21);

                byte[] currentInventory = new byte[320];

                Array.Copy(Bank01to20, 0, currentInventory, 0, 160);
                Array.Copy(Bank21to40, 0, currentInventory, 160, 160);

                int emptyspace = numOfEmpty(currentInventory);

                if (emptyspace < item.Length)
                {
                    DialogResult dialogResult = MyMessageBox.Show("Empty Spaces in your inventory : " + emptyspace + "\n" +
                                                                "Number of items to Spawn : " + item.Length + "\n" +
                                                                "\n" +
                                                                "Press  [Yes]  to clear your inventory and spawn the items " + "\n" +
                                                                "or  [No]  to cancel the spawn." + "\n" + "\n" +
                                                                "[Warning] You will lose your items in your inventory!"
                                                                , "Not enough inventory spaces!", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dialogResult == DialogResult.Yes)
                    {
                        for (int i = 0; i < b1.Length; i++)
                        {
                            b1[i] = data[i];
                            b2[i] = data[i + 160];
                        }

                        Utilities.OverwriteAll(socket, usb, b1, b2, ref counter);
                    }
                    else
                    {
                        hideWait();
                        if (sound)
                            System.Media.SystemSounds.Asterisk.Play();
                        return;
                    }
                }
                else
                {
                    b1 = Bank01to20;
                    b2 = Bank21to40;
                    fillInventory(ref b1, ref b2, item);

                    Utilities.OverwriteAll(socket, usb, b1, b2, ref counter);
                }
            }
            else
            {
                inventorySlot[] SlotPointer = new inventorySlot[40];
                foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                {
                    int slotId = int.Parse(btn.Tag.ToString());
                    SlotPointer[slotId - 1] = btn;
                }
                for (int i = 0; i < SlotPointer.Length; i++)
                {
                    string first = Utilities.flip(Utilities.precedingZeros(SlotPointer[i].getFlag1() + SlotPointer[i].getFlag2() + Utilities.precedingZeros(SlotPointer[i].fillItemID(), 4), 8));
                    string second = Utilities.flip(Utilities.precedingZeros(SlotPointer[i].fillItemData(), 8));
                    Bank = Bank + first + second;
                }

                byte[] currentInventory = new byte[320];

                for (int i = 0; i < Bank.Length / 2 - 1; i++)
                {
                    string tempStr = String.Concat(Bank[(i * 2)].ToString(), Bank[((i * 2) + 1)].ToString());
                    //Debug.Print(i.ToString() + " " + data);
                    currentInventory[i] = Convert.ToByte(tempStr, 16);
                }

                int emptyspace = numOfEmpty(currentInventory);

                if (emptyspace < item.Length)
                {
                    DialogResult dialogResult = MyMessageBox.Show("Empty Spaces in your inventory : " + emptyspace + "\n" +
                                                                "Number of items to Spawn : " + item.Length + "\n" +
                                                                "\n" +
                                                                "Press  [Yes]  to clear your inventory and spawn the new items " + "\n" +
                                                                "or  [No]  to cancel the spawn." + "\n" + "\n" +
                                                                "[Warning] You will lose your items in your inventory!"
                                                                , "Not enough inventory spaces!", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (dialogResult == DialogResult.Yes)
                    {
                        for (int i = 0; i < b1.Length; i++)
                        {
                            b1[i] = data[i];
                            b2[i] = data[i + 160];
                        }
                    }
                    else
                    {
                        hideWait();
                        if (sound)
                            System.Media.SystemSounds.Asterisk.Play();
                        return;
                    }
                }
                else
                {
                    Array.Copy(currentInventory, 0, b1, 0, 160);
                    Array.Copy(currentInventory, 160, b2, 0, 160);

                    fillInventory(ref b1, ref b2, item);
                }
            }

            byte[] newInventory = new byte[320];

            Array.Copy(b1, 0, newInventory, 0, 160);
            Array.Copy(b2, 0, newInventory, 160, 160);

            Invoke((MethodInvoker)delegate
            {
                foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                {
                    if (btn.Tag == null)
                        continue;

                    if (btn.Tag.ToString() == "")
                        continue;

                    int slotId = int.Parse(btn.Tag.ToString());

                    byte[] slotBytes = new byte[2];
                    byte[] flag1Bytes = new byte[1];
                    byte[] flag2Bytes = new byte[1];
                    byte[] dataBytes = new byte[4];
                    byte[] recipeBytes = new byte[2];

                    int slotOffset = ((slotId - 1) * 0x8);
                    int flag1Offset = 0x3 + ((slotId - 1) * 0x8);
                    int flag2Offset = 0x2 + ((slotId - 1) * 0x8);
                    int countOffset = 0x4 + ((slotId - 1) * 0x8);

                    Buffer.BlockCopy(newInventory, slotOffset, slotBytes, 0, 2);
                    Buffer.BlockCopy(newInventory, flag1Offset, flag1Bytes, 0, 1);
                    Buffer.BlockCopy(newInventory, flag2Offset, flag2Bytes, 0, 1);
                    Buffer.BlockCopy(newInventory, countOffset, dataBytes, 0, 4);
                    Buffer.BlockCopy(newInventory, countOffset, recipeBytes, 0, 2);

                    string itemID = Utilities.flip(Utilities.ByteToHexString(slotBytes));
                    string itemData = Utilities.flip(Utilities.ByteToHexString(dataBytes));
                    string recipeData = Utilities.flip(Utilities.ByteToHexString(recipeBytes));
                    string flag1 = Utilities.ByteToHexString(flag1Bytes);
                    string flag2 = Utilities.ByteToHexString(flag2Bytes);

                    //Debug.Print("Slot : " + slotId.ToString() + " ID : " + itemID + " Data : " + itemData + " recipeData : " + recipeData + " Flag1 : " + flag1 + " Flag2 : " + flag2);

                    if (itemID == "FFFE") //Nothing
                    {
                        btn.setup("", 0xFFFE, 0x0, "", "00", "00");
                        continue;
                    }
                    else if (itemID == "16A2") //Recipe
                    {
                        btn.setup(GetNameFromID(recipeData, recipeSource), 0x16A2, Convert.ToUInt32("0x" + itemData, 16), GetImagePathFromID(recipeData, recipeSource), "", flag1, flag2);
                        continue;
                    }
                    else if (itemID == "1095") //Delivery
                    {
                        btn.setup(GetNameFromID(recipeData, itemSource), 0x1095, Convert.ToUInt32("0x" + itemData, 16), GetImagePathFromID(recipeData, itemSource, Convert.ToUInt32("0x" + itemData, 16)), "", flag1, flag2);
                        continue;
                    }
                    else if (itemID == "16A1") //Bottle Message
                    {
                        btn.setup(GetNameFromID(recipeData, recipeSource), 0x16A1, Convert.ToUInt32("0x" + itemData, 16), GetImagePathFromID(recipeData, recipeSource), "", flag1, flag2);
                        continue;
                    }
                    else if (itemID == "0A13") // Fossil
                    {
                        btn.setup(GetNameFromID(recipeData, itemSource), 0x0A13, Convert.ToUInt32("0x" + itemData, 16), GetImagePathFromID(recipeData, itemSource), "", flag1, flag2);
                        continue;
                    }
                    else if (itemID == "114A") // Money Tree
                    {
                        btn.setup(GetNameFromID(itemID, itemSource), Convert.ToUInt16("0x" + itemID, 16), Convert.ToUInt32("0x" + itemData, 16), GetImagePathFromID(itemID, itemSource, Convert.ToUInt32("0x" + itemData, 16)), GetNameFromID(recipeData, itemSource), flag1, flag2);
                        continue;
                    }
                    else
                    {
                        btn.setup(GetNameFromID(itemID, itemSource), Convert.ToUInt16("0x" + itemID, 16), Convert.ToUInt32("0x" + itemData, 16), GetImagePathFromID(itemID, itemSource, Convert.ToUInt32("0x" + itemData, 16)), "", flag1, flag2);
                        continue;
                    }
                }
            });

            hideWait();
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private byte[][] processNHI(byte[] data)
        {
            byte[] tempItem = new byte[8];
            bool[] isItem = new bool[40];
            int numOfitem = 0;

            for (int i = 0; i < 40; i++)
            {
                Buffer.BlockCopy(data, 0x8 * i, tempItem, 0, 8);
                if (!Utilities.ByteToHexString(tempItem).Equals("FEFF000000000000"))
                {
                    isItem[i] = true;
                    numOfitem++;
                }
            }

            byte[][] item = new byte[numOfitem][];
            int itemNum = 0;
            for (int j = 0; j < 40; j++)
            {
                if (isItem[j])
                {
                    item[itemNum] = new byte[8];
                    Buffer.BlockCopy(data, 0x8 * j, item[itemNum], 0, 8);
                    itemNum++;
                }
            }

            return item;
        }

        private int numOfEmpty(byte[] data)
        {
            byte[] tempItem = new byte[8];
            int num = 0;

            for (int i = 0; i < 40; i++)
            {
                Buffer.BlockCopy(data, 0x8 * i, tempItem, 0, 8);
                if (Utilities.ByteToHexString(tempItem).Equals("FEFF000000000000"))
                    num++;
            }
            return num;
        }

        private void fillInventory(ref byte[] b1, ref byte[] b2, byte[][] item)
        {
            byte[] tempItem = new byte[8];
            int num = 0;

            for (int i = 0; i < 20; i++)
            {
                Buffer.BlockCopy(b1, 0x8 * i, tempItem, 0, 8);
                if (Utilities.ByteToHexString(tempItem).Equals("FEFF000000000000"))
                {
                    Buffer.BlockCopy(item[num], 0, b1, 0x8 * i, 8);
                    num++;
                }
                if (num >= item.Length)
                    return;
            }

            for (int j = 0; j < 20; j++)
            {
                Buffer.BlockCopy(b2, 0x8 * j, tempItem, 0, 8);
                if (Utilities.ByteToHexString(tempItem).Equals("FEFF000000000000"))
                {
                    Buffer.BlockCopy(item[num], 0, b2, 0x8 * j, 8);
                    num++;
                }
                if (num >= item.Length)
                    return;
            }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            UpdateInventory();
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void FillRemainButton_Click(object sender, EventArgs e)
        {
            if (IDTextbox.Text == "")
            {
                MessageBox.Show("Please enter an ID before sending item");
                return;
            }

            if (AmountOrCountTextbox.Text == "")
            {
                MessageBox.Show("Please enter an amount");
                return;
            }

            string itemID = IDTextbox.Text;

            if (HexModeButton.Tag.ToString() == "Normal")
            {
                int decValue = Convert.ToInt32(AmountOrCountTextbox.Text) - 1;
                string itemAmount = decValue.ToString("X");
                Thread fillRemainThread = new Thread(delegate () { fillRemain(itemID, itemAmount); });
                fillRemainThread.Start();
            }
            else
            {
                string itemAmount = AmountOrCountTextbox.Text;
                Thread fillRemainThread = new Thread(delegate () { fillRemain(itemID, itemAmount); });
                fillRemainThread.Start();
            }
            //this.ShowMessage(IDTextbox.Text);
        }

        private void fillRemain(string itemID, string itemAmount)
        {
            lock (itemLock)
            {
                showWait();

                if (!offline)
                {
                    try
                    {
                        byte[] Bank01to20 = Utilities.GetInventoryBank(socket, usb, 1);
                        byte[] Bank21to40 = Utilities.GetInventoryBank(socket, usb, 21);

                        foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                        {
                            int slot = int.Parse(btn.Tag.ToString());
                            byte[] slotBytes = new byte[2];

                            int slotOffset;
                            if (slot < 21)
                            {
                                slotOffset = ((slot - 1) * 0x8);
                            }
                            else
                            {
                                slotOffset = ((slot - 21) * 0x8);
                            }

                            if (slot < 21)
                            {
                                Buffer.BlockCopy(Bank01to20, slotOffset, slotBytes, 0x0, 0x2);
                            }
                            else
                            {
                                Buffer.BlockCopy(Bank21to40, slotOffset, slotBytes, 0x0, 0x2);
                            }

                            string slotID = Utilities.flip(Utilities.ByteToHexString(slotBytes));

                            if (slotID == "FFFE")
                            {
                                Utilities.SpawnItem(socket, usb, slot, SelectedItem.getFlag1() + SelectedItem.getFlag2() + itemID, itemAmount);
                                Invoke((MethodInvoker)delegate
                                {
                                    if (itemID == "16A2") //Recipe
                                    {
                                        btn.setup(GetNameFromID(Utilities.turn2bytes(itemAmount), recipeSource), 0x16A2, Convert.ToUInt32("0x" + itemAmount, 16), GetImagePathFromID(Utilities.turn2bytes(itemAmount), recipeSource), "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
                                    }
                                    else
                                    {
                                        btn.setup(GetNameFromID(itemID, itemSource), Convert.ToUInt16("0x" + itemID, 16), Convert.ToUInt32("0x" + itemAmount, 16), GetImagePathFromID(itemID, itemSource, Convert.ToUInt32("0x" + itemAmount, 16)), "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
                                    }
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MyLog.logEvent("MainForm", "FillRemain: " + ex.Message.ToString());
                        MyMessageBox.Show(ex.Message.ToString(), "This code didn't port easily. WTF does it do?");
                    }

                    Thread.Sleep(3000);
                }
                else
                {
                    foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                    {
                        if (btn.fillItemID() == "FFFE")
                        {
                            Invoke((MethodInvoker)delegate
                            {
                                if (itemID == "16A2") //Recipe
                                {
                                    btn.setup(GetNameFromID(Utilities.turn2bytes(itemAmount), recipeSource), 0x16A2, Convert.ToUInt32("0x" + itemAmount, 16), GetImagePathFromID(Utilities.turn2bytes(itemAmount), recipeSource), "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
                                }
                                else
                                {
                                    btn.setup(GetNameFromID(itemID, itemSource), Convert.ToUInt16("0x" + itemID, 16), Convert.ToUInt32("0x" + itemAmount, 16), GetImagePathFromID(itemID, itemSource, Convert.ToUInt32("0x" + itemAmount, 16)), "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
                                }
                            });
                        }
                    }
                }
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
                hideWait();
            }
        }

        private void SpawnAllButton_Click(object sender, EventArgs e)
        {
            if (IDTextbox.Text == "")
            {
                MessageBox.Show("Please enter an ID before sending item");
                return;
            }

            if (AmountOrCountTextbox.Text == "")
            {
                MessageBox.Show("Please enter an amount");
                return;
            }

            string itemID = SelectedItem.getFlag1() + SelectedItem.getFlag2() + IDTextbox.Text;

            if (HexModeButton.Tag.ToString() == "Normal")
            {
                int decValue = Convert.ToInt32(AmountOrCountTextbox.Text) - 1;
                string itemAmount;
                if (decValue < 0)
                    itemAmount = "0";
                else
                    itemAmount = decValue.ToString("X");
                Thread spawnAllThread = new Thread(delegate () { spawnAll(itemID, itemAmount); });
                spawnAllThread.Start();
            }
            else
            {
                string itemAmount = AmountOrCountTextbox.Text;
                Thread spawnAllThread = new Thread(delegate () { spawnAll(itemID, itemAmount); });
                spawnAllThread.Start();
            }
            //this.ShowMessage(IDTextbox.Text);
        }

        private void spawnAll(string itemID, string itemAmount)
        {
            showWait();

            if (!offline)
            {
                byte[] b = new byte[160];
                byte[] ID = Utilities.stringToByte(Utilities.flip(Utilities.precedingZeros(itemID, 8)));
                byte[] Data = Utilities.stringToByte(Utilities.flip(Utilities.precedingZeros(itemAmount, 8)));

                //Debug.Print(Utilities.precedingZeros(itemID, 8));
                //Debug.Print(Utilities.precedingZeros(itemAmount, 8));

                for (int i = 0; i < b.Length; i += 8)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        b[i + j] = ID[j];
                        b[i + j + 4] = Data[j];
                    }
                }

                //string result = Encoding.ASCII.GetString(Utilities.transform(b));
                //Debug.Print(result);
                try
                {
                    Utilities.OverwriteAll(socket, usb, b, b, ref counter);
                }
                catch (Exception ex)
                {
                    MyLog.logEvent("MainForm", "SpawnAll: " + ex.Message.ToString());
                    MyMessageBox.Show(ex.Message.ToString(), "Multithreading badness. This will cause a crash later!");
                }

                foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                {
                    Invoke((MethodInvoker)delegate
                    {
                        if (Utilities.turn2bytes(itemID) == "16A2") //Recipe
                        {
                            btn.setup(GetNameFromID(Utilities.turn2bytes(itemAmount), recipeSource), 0x16A2, Convert.ToUInt32("0x" + itemAmount, 16), GetImagePathFromID(Utilities.turn2bytes(itemAmount), recipeSource), "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
                        }
                        else
                        {
                            btn.setup(GetNameFromID(Utilities.turn2bytes(itemID), itemSource), Convert.ToUInt16("0x" + Utilities.turn2bytes(itemID), 16), Convert.ToUInt32("0x" + itemAmount, 16), GetImagePathFromID(Utilities.turn2bytes(itemID), itemSource, Convert.ToUInt32("0x" + itemAmount, 16)), "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
                        }
                    });
                }

                Thread.Sleep(1000);
            }
            else
            {
                foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                {
                    Invoke((MethodInvoker)delegate
                    {
                        if (Utilities.turn2bytes(itemID) == "16A2") //Recipe
                        {
                            btn.setup(GetNameFromID(Utilities.turn2bytes(itemAmount), recipeSource), 0x16A2, Convert.ToUInt32("0x" + itemAmount, 16), GetImagePathFromID(Utilities.turn2bytes(itemAmount), recipeSource), "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
                        }
                        else
                        {
                            btn.setup(GetNameFromID(Utilities.turn2bytes(itemID), itemSource), Convert.ToUInt16("0x" + Utilities.turn2bytes(itemID), 16), Convert.ToUInt32("0x" + itemAmount, 16), GetImagePathFromID(Utilities.turn2bytes(itemID), itemSource, Convert.ToUInt32("0x" + itemAmount, 16)), "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
                        }
                    });
                }
            }
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
            hideWait();
        }

        private void ClearAllButton_Click(object sender, EventArgs e)
        {
            Thread clearThread = new Thread(clearInventory);
            clearThread.Start();
        }

        private void clearInventory()
        {
            showWait();

            try
            {
                if (!offline)
                {
                    byte[] b = new byte[160];

                    //Debug.Print(Utilities.precedingZeros(itemID, 8));
                    //Debug.Print(Utilities.precedingZeros(itemAmount, 8));

                    for (int i = 0; i < b.Length; i += 8)
                    {
                        b[i] = 0xFE;
                        b[i + 1] = 0xFF;
                        for (int j = 0; j < 6; j++)
                        {
                            b[i + 2 + j] = 0x00;
                        }
                    }

                    Utilities.OverwriteAll(socket, usb, b, b, ref counter);
                    //string result = Encoding.ASCII.GetString(Utilities.transform(b));
                    //Debug.Print(result);

                    foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                    {
                        Invoke((MethodInvoker)delegate
                        {
                            btn.reset();
                        });
                    }
                    Invoke((MethodInvoker)delegate
                    {
                        ButtonToolTip.RemoveAll();
                    });
                    Thread.Sleep(1000);
                }
                else
                {
                    foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                    {
                        Invoke((MethodInvoker)delegate
                        {
                            btn.reset();
                        });
                    }
                    Invoke((MethodInvoker)delegate
                    {
                        ButtonToolTip.RemoveAll();
                    });
                }
            }
            catch (Exception ex)
            {
                MyLog.logEvent("MainForm", "ClearInventory: " + ex.Message.ToString());
                MyMessageBox.Show(ex.Message.ToString(), "This is catastrophically bad, don't do this. Someone needs to fix this.");
            }
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
            hideWait();
        }

        private void InventoryTabButton_Click(object sender, EventArgs e)
        {
            this.InventoryTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            this.CritterTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.OtherTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.VillagerTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            InventoryLargePanel.Visible = true;
            OtherLargePanel.Visible = false;
            CritterLargePanel.Visible = false;
            VillagerLargePanel.Visible = false;
        }

        private void OtherTabButton_Click(object sender, EventArgs e)
        {
            this.InventoryTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.CritterTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.OtherTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            this.VillagerTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            InventoryLargePanel.Visible = false;
            OtherLargePanel.Visible = true;
            CritterLargePanel.Visible = false;
            VillagerLargePanel.Visible = false;
            closeVariationMenu();
        }

        private void CritterTabButton_Click(object sender, EventArgs e)
        {
            this.InventoryTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.CritterTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            this.OtherTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.VillagerTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            InventoryLargePanel.Visible = false;
            OtherLargePanel.Visible = false;
            CritterLargePanel.Visible = true;
            VillagerLargePanel.Visible = false;
            closeVariationMenu();
        }

        private void VillagerTabButton_Click(object sender, EventArgs e)
        {
            this.InventoryTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.CritterTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.OtherTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            this.VillagerTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));

            InventoryLargePanel.Visible = false;
            OtherLargePanel.Visible = false;
            CritterLargePanel.Visible = false;
            VillagerLargePanel.Visible = true;
            closeVariationMenu();

            if (V == null && VillagerFirstLoad)
            {
                VillagerFirstLoad = true;
                Thread LoadAllVillagerThread = new Thread(delegate () { loadAllVillager(); });
                LoadAllVillagerThread.Start();
            }
        }

        private void MapDropperButton_Click(object sender, EventArgs e)
        {
            ItemSearchBox.Clear();

            if (M == null)
            {
                M = new map(socket, usb, Utilities.itemPath, Utilities.recipePath, Utilities.flowerPath, Utilities.variationPath, Utilities.favPath, Utilities.imagePath, languageSetting, OverrideDict, sound);
                M.closeForm += M_closeForm;
                M.Show();
            }
        }

        private void M_closeForm()
        {
            M = null;
        }

        private void RegeneratorButton_Click(object sender, EventArgs e)
        {
            if (R == null)
            {
                R = new MapRegenerator(socket, sound);
                R.closeForm += R_closeForm;
                R.Show();
            }
        }

        private void R_closeForm()
        {
            R = null;
        }

        private void BulldozerButton_Click(object sender, EventArgs e)
        {
            if (B == null)
            {
                B = new Bulldozer(socket, usb, sound);
                B.closeForm += B_closeForm;
                B.Show();
            }
        }

        private void B_closeForm()
        {
            B = null;
        }

        private void FreezerButton_Click(object sender, EventArgs e)
        {
            if (F == null)
            {
                F = new Freezer(socket, sound);
                F.closeForm += F_closeForm;
                F.Show();
            }
        }

        private void F_closeForm()
        {
            F = null;
        }

        private void DodoHelperButton_Click(object sender, EventArgs e)
        {
            if (D == null)
            {
                D = new dodo(socket, true)
                {
                    ControlBox = true,
                    ShowInTaskbar = true
                };
                D.closeForm += D_closeForm;
                D.abortAll += D_abortAll;
                D.Show();
                D.WriteLog("[You have started dodo helper in standalone mode.]\n\n" +
                                    "1. Disconnect all controller by selecting \"Controllers\" > \"Change Grip/Order\"\n" +
                                    "2. Leave only the Joy-Con docked on your Switch.\n" +
                                    "3. Return to the game and dock your Switch if needed. Try pressing the buttons below to test the virtual controller.\n" +
                                    "4. If the virtual controller does not response, try the \"Detach\" button first, then the \"A\" button.\n" +
                                    "5. If the virtual controller still does not appear, try restart your Switch.\n\n" +
                                    ">> Please try the buttons below to test the virtual controller. <<"
                                    );
            }
        }

        private void D_abortAll()
        {
            MyMessageBox.Show("Dodo Helper Aborted!\nPlease remember to exit the airport first if you want to restart!", "Slamming on the brakes?", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void D_closeForm()
        {
            D = null;
        }

        private void Hex_KeyPress(object sender, KeyPressEventArgs e)
        {
            char c = e.KeyChar;
            if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
            {
                e.Handled = true;
            }
            if (c >= 'a' && c <= 'f') e.KeyChar = char.ToUpper(c);
        }

        private void Dec_KeyPress(object sender, KeyPressEventArgs e)
        {
            char c = e.KeyChar;
            if (!((c >= '0' && c <= '9')))
            {
                e.Handled = true;
            }
        }

        private void DecAndHex_KeyPress(object sender, KeyPressEventArgs e)
        {
            char c = e.KeyChar;
            if (HexModeButton.Tag.ToString() == "Normal")
            {
                if (!((c >= '0' && c <= '9')))
                {
                    e.Handled = true;
                }
            }
            else
            {
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
                {
                    e.Handled = true;
                }
                if (c >= 'a' && c <= 'f') e.KeyChar = char.ToUpper(c);
            }
        }

        private void ItemSearchBox_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (ItemGridView.DataSource != null)
                    (ItemGridView.DataSource as DataTable).DefaultView.RowFilter = string.Format(languageSetting + " LIKE '%{0}%'", EscapeLikeValue(ItemSearchBox.Text));
                if (RecipeGridView.DataSource != null)
                    (RecipeGridView.DataSource as DataTable).DefaultView.RowFilter = string.Format(languageSetting + " LIKE '%{0}%'", EscapeLikeValue(ItemSearchBox.Text));
                if (FlowerGridView.DataSource != null)
                    (FlowerGridView.DataSource as DataTable).DefaultView.RowFilter = string.Format(languageSetting + " LIKE '%{0}%'", EscapeLikeValue(ItemSearchBox.Text));
                if (FavGridView.DataSource != null)
                    (FavGridView.DataSource as DataTable).DefaultView.RowFilter = string.Format("Name" + " LIKE '%{0}%'", EscapeLikeValue(ItemSearchBox.Text));
            }
            catch
            {
                ItemSearchBox.Clear();
            }
        }

        public static string EscapeLikeValue(string valueWithoutWildcards)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < valueWithoutWildcards.Length; i++)
            {
                char c = valueWithoutWildcards[i];
                if (c == '*' || c == '%' || c == '[' || c == ']')
                    sb.Append("[").Append(c).Append("]");
                else if (c == '\'')
                    sb.Append("''");
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private void ItemSearchBox_Click(object sender, EventArgs e)
        {
            if (ItemSearchBox.Text == "Search...")
            {
                ItemSearchBox.Text = "";
                ItemSearchBox.ForeColor = Color.White;
            }
        }

        private void RecipeGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < this.RecipeGridView.Rows.Count)
            {
                if (e.ColumnIndex == 13)
                {
                    string imageName = RecipeGridView.Rows[e.RowIndex].Cells["iName"].Value.ToString();
                    string path;

                    if (OverrideDict.ContainsKey(imageName))
                    {
                        path = Utilities.imagePath + OverrideDict[imageName] + ".png";
                        if (File.Exists(path))
                        {
                            Image img = Image.FromFile(path);
                            //e.CellStyle.BackColor = Color.Green;
                            e.Value = img;

                            return;
                        }
                    }

                    path = Utilities.imagePath + imageName + ".png";
                    if (File.Exists(path))
                    {
                        Image img = Image.FromFile(path);
                        e.Value = img;
                    }
                    else
                    {
                        path = Utilities.imagePath + imageName + "_Remake_0_0.png";
                        if (File.Exists(path))
                        {
                            Image img = Image.FromFile(path);
                            e.CellStyle.BackColor = Color.FromArgb(((int)(((byte)(56)))), ((int)(((byte)(77)))), ((int)(((byte)(162)))));
                            e.Value = img;
                        }
                        else
                        {
                            e.CellStyle.BackColor = Color.Red;
                        }
                    }
                }
            }
        }

        private void RecipeGridView_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (recipelastRow != null)
            {
                recipelastRow.Height = 22;
            }

            if (e.RowIndex > -1)
            {
                recipelastRow = RecipeGridView.Rows[e.RowIndex];
                RecipeGridView.Rows[e.RowIndex].Height = 128;
                RecipeIDTextbox.Text = RecipeGridView.Rows[e.RowIndex].Cells["id"].Value.ToString();

                SelectedItem.setup(RecipeGridView.Rows[e.RowIndex].Cells[languageSetting].Value.ToString(), 0x16A2, Convert.ToUInt32("0x" + RecipeGridView.Rows[e.RowIndex].Cells["id"].Value.ToString(), 16), GetImagePathFromID(RecipeGridView.Rows[e.RowIndex].Cells["id"].Value.ToString(), recipeSource), true);
                updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
            }
        }

        private void FlowerGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < this.FlowerGridView.Rows.Count)
            {
                if (e.ColumnIndex == 13)
                {
                    string imageName = FlowerGridView.Rows[e.RowIndex].Cells["iName"].Value.ToString();

                    if (OverrideDict.ContainsKey(imageName))
                    {
                        string path = Utilities.imagePath + OverrideDict[imageName] + ".png";
                        if (File.Exists(path))
                        {
                            Image img = Image.FromFile(path);
                            //e.CellStyle.BackColor = Color.Green;
                            e.Value = img;

                            return;
                        }
                    }
                    else
                    {
                        e.CellStyle.BackColor = Color.Red;
                    }
                }
            }
        }

        private void FlowerGridView_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (flowerlastRow != null)
            {
                flowerlastRow.Height = 22;
            }
            if (e.RowIndex > -1)
            {
                flowerlastRow = FlowerGridView.Rows[e.RowIndex];
                FlowerGridView.Rows[e.RowIndex].Height = 128;
                FlowerIDTextbox.Text = FlowerGridView.Rows[e.RowIndex].Cells["id"].Value.ToString();
                FlowerValueTextbox.Text = FlowerGridView.Rows[e.RowIndex].Cells["value"].Value.ToString();

                SelectedItem.setup(FlowerGridView.Rows[e.RowIndex].Cells[languageSetting].Value.ToString(), Convert.ToUInt16("0x" + FlowerGridView.Rows[e.RowIndex].Cells["id"].Value.ToString(), 16), Convert.ToUInt32("0x" + FlowerGridView.Rows[e.RowIndex].Cells["value"].Value.ToString(), 16), GetImagePathFromID(FlowerGridView.Rows[e.RowIndex].Cells["id"].Value.ToString(), itemSource), true);
                updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
            }
        }

        private void FavGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < this.FavGridView.Rows.Count)
            {
                if (e.ColumnIndex == 4)
                {
                    string imageName = FavGridView.Rows[e.RowIndex].Cells["iName"].Value.ToString();
                    string path;

                    if (OverrideDict.ContainsKey(imageName))
                    {
                        path = Utilities.imagePath + OverrideDict[imageName] + ".png";
                        if (File.Exists(path))
                        {
                            Image img = Image.FromFile(path);
                            //e.CellStyle.BackColor = Color.Green;
                            e.Value = img;

                            return;
                        }
                    }

                    path = Utilities.imagePath + imageName + ".png";
                    if (File.Exists(path))
                    {
                        Image img = Image.FromFile(path);
                        e.Value = img;
                    }
                    else
                    {
                        path = Utilities.imagePath + imageName + "_Remake_0_0.png";
                        if (File.Exists(path))
                        {
                            Image img = Image.FromFile(path);
                            e.CellStyle.BackColor = Color.FromArgb(((int)(((byte)(56)))), ((int)(((byte)(77)))), ((int)(((byte)(162)))));
                            e.Value = img;
                        }
                        else
                        {
                            path = Utilities.imagePath + removeNumber(imageName) + ".png";
                            if (File.Exists(path))
                            {
                                Image img = Image.FromFile(path);
                                e.Value = img;
                            }
                            else
                            {
                                e.CellStyle.BackColor = Color.Red;
                            }
                        }
                    }
                }
            }
        }

        private void FavGridView_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            MouseEventArgs me = (MouseEventArgs)e;
            if (me.Button == System.Windows.Forms.MouseButtons.Left)
            {
                if (favlastRow != null)
                {
                    favlastRow.Height = 22;
                }
                if (e.RowIndex > -1)
                {
                    favlastRow = FavGridView.Rows[e.RowIndex];
                    FavGridView.Rows[e.RowIndex].Height = 128;
                    if (HexModeButton.Tag.ToString() == "Normal")
                    {
                        HexModeButton_Click(sender, e);
                    }

                    string id = FavGridView.Rows[e.RowIndex].Cells["id"].Value.ToString();
                    string iName = FavGridView.Rows[e.RowIndex].Cells["iName"].Value.ToString();
                    string name = FavGridView.Rows[e.RowIndex].Cells["Name"].Value.ToString();
                    string data = FavGridView.Rows[e.RowIndex].Cells["value"].Value.ToString();

                    AmountOrCountTextbox.Text = Utilities.precedingZeros(data, 8);
                    IDTextbox.Text = Utilities.precedingZeros(id, 4);

                    string hexValue = Utilities.precedingZeros(data, 8);
                    UInt16 IntId = Convert.ToUInt16("0x" + id, 16);

                    string front = Utilities.precedingZeros(hexValue, 8).Substring(0, 4);
                    string back = Utilities.precedingZeros(hexValue, 8).Substring(4, 4);

                    if (id == "16A2") //recipe
                    {
                        SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(hexValue), recipeSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(hexValue), recipeSource), true, "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
                    }
                    else if (id == "315A" || id == "1618" || id == "342F") // Wall-Mounted
                    {
                        SelectedItem.setup(GetNameFromID(IDTextbox.Text, itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(IDTextbox.Text, itemSource, Convert.ToUInt32("0x" + hexValue, 16)), true, GetImagePathFromID((Utilities.turn2bytes(hexValue)), itemSource, Convert.ToUInt32("0x" + Utilities.translateVariationValueBack(front), 16)), SelectedItem.getFlag1(), SelectedItem.getFlag2());
                    }
                    else if (ItemAttr.hasFenceWithVariation(IntId))  // Fence Variation
                    {
                        SelectedItem.setup(name, Convert.ToUInt16("0x" + id, 16), Convert.ToUInt32("0x" + data, 16), GetImagePathFromID(id, itemSource, Convert.ToUInt32("0x" + front, 16)), true, "");
                    }
                    else
                    {
                        SelectedItem.setup(name, Convert.ToUInt16("0x" + id, 16), Convert.ToUInt32("0x" + data, 16), GetImagePathFromID(id, itemSource, Convert.ToUInt32("0x" + data, 16)), true, "");
                    }

                    if (selection != null)
                    {
                        selection.receiveID(Utilities.precedingZeros(SelectedItem.fillItemID(), 4), languageSetting, Utilities.precedingZeros(hexValue, 8));
                    }
                    updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
                }
            }
        }

        private void PlayerInventorySelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (PlayerInventorySelector.SelectedIndex < 0)
                return;

            switch (PlayerInventorySelector.SelectedIndex)
            {
                case 0:
                    Utilities.setAddress(1);
                    maxPage = 1;
                    currentPage = 1;
                    hidePagination();
                    UpdateInventory();
                    break;
                case 1:
                    Utilities.setAddress(2);
                    maxPage = 1;
                    currentPage = 1;
                    hidePagination();
                    UpdateInventory();
                    break;
                case 2:
                    Utilities.setAddress(3);
                    maxPage = 1;
                    currentPage = 1;
                    hidePagination();
                    UpdateInventory();
                    break;
                case 3:
                    Utilities.setAddress(4);
                    maxPage = 1;
                    currentPage = 1;
                    hidePagination();
                    UpdateInventory();
                    break;
                case 4:
                    Utilities.setAddress(5);
                    maxPage = 1;
                    currentPage = 1;
                    hidePagination();
                    UpdateInventory();
                    break;
                case 5:
                    Utilities.setAddress(6);
                    maxPage = 1;
                    currentPage = 1;
                    hidePagination();
                    UpdateInventory();
                    break;
                case 6:
                    Utilities.setAddress(7);
                    maxPage = 1;
                    currentPage = 1;
                    hidePagination();
                    UpdateInventory();
                    break;
                case 7:
                    Utilities.setAddress(8);
                    maxPage = 1;
                    currentPage = 1;
                    hidePagination();
                    UpdateInventory();
                    break;
                case 8: // House
                    Utilities.setAddress(11);
                    maxPage = 125;
                    currentPage = 1;
                    showPagination();
                    UpdateInventory();
                    break;
                case 9:
                    Utilities.setAddress(12);
                    maxPage = 125;
                    currentPage = 1;
                    showPagination();
                    UpdateInventory();
                    break;
                case 10:
                    Utilities.setAddress(13);
                    maxPage = 125;
                    currentPage = 1;
                    showPagination();
                    UpdateInventory();
                    break;
                case 11:
                    Utilities.setAddress(14);
                    maxPage = 125;
                    currentPage = 1;
                    showPagination();
                    UpdateInventory();
                    break;
                case 12:
                    Utilities.setAddress(15);
                    maxPage = 125;
                    currentPage = 1;
                    showPagination();
                    UpdateInventory();
                    break;
                case 13:
                    Utilities.setAddress(16);
                    maxPage = 125;
                    currentPage = 1;
                    showPagination();
                    UpdateInventory();
                    break;
                case 14:
                    Utilities.setAddress(17);
                    maxPage = 125;
                    currentPage = 1;
                    showPagination();
                    UpdateInventory();
                    break;
                case 15:
                    Utilities.setAddress(18);
                    maxPage = 125;
                    currentPage = 1;
                    showPagination();
                    UpdateInventory();
                    break;
                case 16: // Re
                    Utilities.setAddress(9);
                    maxPage = 2;
                    currentPage = 1;
                    showPagination();
                    UpdateInventory();
                    break;
            }
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void FastBackButton_Click(object sender, EventArgs e)
        {
            if (currentPage == 1)
            {
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
                return;
            }
            if (currentPage - 10 > 1)
            {
                if (PlayerInventorySelector.SelectedIndex == 16)
                {
                    Utilities.gotoRecyclingPage((uint)(currentPage - 10));
                }
                else if (PlayerInventorySelector.SelectedIndex > 7)
                {
                    Utilities.gotoHousePage((uint)(currentPage - 10), PlayerInventorySelector.SelectedIndex - 7);
                }
                currentPage -= 10;
            }
            else
            {
                if (PlayerInventorySelector.SelectedIndex == 16)
                {
                    Utilities.gotoRecyclingPage(1);
                }
                else if (PlayerInventorySelector.SelectedIndex > 7)
                {
                    Utilities.gotoHousePage(1, PlayerInventorySelector.SelectedIndex - 7);
                }
                currentPage = 1;
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
            setPageLabel();
            UpdateInventory();
        }

        private void BackButton_Click(object sender, EventArgs e)
        {
            if (currentPage > 1)
            {
                if (PlayerInventorySelector.SelectedIndex == 16)
                {
                    Utilities.gotoRecyclingPage((uint)(currentPage - 1));
                }
                else if (PlayerInventorySelector.SelectedIndex > 7)
                {
                    Utilities.gotoHousePage((uint)(currentPage - 1), PlayerInventorySelector.SelectedIndex - 7);
                }
                currentPage--;
                setPageLabel();
                UpdateInventory();
            }
            else
            {
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
        }

        private void NextButton_Click(object sender, EventArgs e)
        {
            if (currentPage < maxPage)
            {
                if (PlayerInventorySelector.SelectedIndex == 16)
                {
                    Utilities.gotoRecyclingPage((uint)(currentPage + 1));
                }
                else if (PlayerInventorySelector.SelectedIndex > 7)
                {
                    Utilities.gotoHousePage((uint)(currentPage + 1), PlayerInventorySelector.SelectedIndex - 7);
                }
                currentPage++;
                setPageLabel();
                UpdateInventory();
            }
            else
            {
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
        }

        private void FastNextButton_Click(object sender, EventArgs e)
        {
            if (currentPage == 1)
            {
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
                return;
            }
            if (currentPage - 10 > 1)
            {
                if (PlayerInventorySelector.SelectedIndex == 16)
                {
                    Utilities.gotoRecyclingPage((uint)(currentPage - 10));
                }
                else if (PlayerInventorySelector.SelectedIndex > 7)
                {
                    Utilities.gotoHousePage((uint)(currentPage - 10), PlayerInventorySelector.SelectedIndex - 7);
                }
                currentPage -= 10;
            }
            else
            {
                if (PlayerInventorySelector.SelectedIndex == 16)
                {
                    Utilities.gotoRecyclingPage(1);
                }
                else if (PlayerInventorySelector.SelectedIndex > 7)
                {
                    Utilities.gotoHousePage(1, PlayerInventorySelector.SelectedIndex - 7);
                }
                currentPage = 1;
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
            setPageLabel();
            UpdateInventory();
        }

        private void setPageLabel()
        {
            PageLabel.Text = "Page " + currentPage;
        }

        private void showPagination()
        {
            setPageLabel();
            PaginationPanel.Visible = true;
        }

        private void hidePagination()
        {
            setPageLabel();
            PaginationPanel.Visible = false;
        }

        private void IDTextbox_TextChanged(object sender, EventArgs e)
        {
            if (((RichTextBox)sender).Modified)
            {
                if (IDTextbox.Text != "")
                {
                    if (ItemAttr.hasGenetics(Convert.ToUInt16("0x" + IDTextbox.Text, 16)))
                    {
                        if (HexModeButton.Tag.ToString() == "Normal")
                        {
                            HexModeButton_Click(sender, e);
                        }

                        string value = AmountOrCountTextbox.Text;
                        int length = value.Length;
                        string firstByte;
                        string secondByte;
                        if (length < 2)
                        {
                            firstByte = "0";
                            secondByte = value;
                        }
                        else
                        {
                            firstByte = value.Substring(length - 2, 1);
                            secondByte = value.Substring(length - 1, 1);
                        }

                        setGeneComboBox(firstByte, secondByte);
                        GenePanel.Visible = true;
                    }
                    else
                    {
                        GenePanel.Visible = false;
                    }

                    if (IDTextbox.Text == "315A" || IDTextbox.Text == "1618" || IDTextbox.Text == "342F")
                    {
                        WallMountMsg.Visible = true;
                    }
                    else
                    {
                        WallMountMsg.Visible = false;
                    }
                }
                else
                {
                    GenePanel.Visible = false;
                    WallMountMsg.Visible = false;
                }
            }
            else
            {
                GenePanel.Visible = false;
                WallMountMsg.Visible = false;
            }
        }

        #region Gene
        private void setGeneComboBox(string firstByte, string secondByte)
        {
            switch (firstByte)
            {
                case "0":
                    FlowerGeneW.SelectedIndex = 0;
                    FlowerGeneS.SelectedIndex = 0;
                    break;
                case "1":
                    FlowerGeneW.SelectedIndex = 1;
                    FlowerGeneS.SelectedIndex = 0;
                    break;
                case "3":
                    FlowerGeneW.SelectedIndex = 2;
                    FlowerGeneS.SelectedIndex = 0;
                    break;
                case "4":
                    FlowerGeneW.SelectedIndex = 0;
                    FlowerGeneS.SelectedIndex = 1;
                    break;
                case "5":
                    FlowerGeneW.SelectedIndex = 1;
                    FlowerGeneS.SelectedIndex = 1;
                    break;
                case "7":
                    FlowerGeneW.SelectedIndex = 2;
                    FlowerGeneS.SelectedIndex = 1;
                    break;
                case "C":
                    FlowerGeneW.SelectedIndex = 0;
                    FlowerGeneS.SelectedIndex = 2;
                    break;
                case "D":
                    FlowerGeneW.SelectedIndex = 1;
                    FlowerGeneS.SelectedIndex = 2;
                    break;
                case "F":
                    FlowerGeneW.SelectedIndex = 2;
                    FlowerGeneS.SelectedIndex = 2;
                    break;
                default:
                    FlowerGeneW.SelectedIndex = -1;
                    FlowerGeneS.SelectedIndex = -1;
                    break;
            }
            switch (secondByte)
            {
                case "0":
                    FlowerGeneR.SelectedIndex = 0;
                    FlowerGeneY.SelectedIndex = 0;
                    break;
                case "1":
                    FlowerGeneR.SelectedIndex = 1;
                    FlowerGeneY.SelectedIndex = 0;
                    break;
                case "3":
                    FlowerGeneR.SelectedIndex = 2;
                    FlowerGeneY.SelectedIndex = 0;
                    break;
                case "4":
                    FlowerGeneR.SelectedIndex = 0;
                    FlowerGeneY.SelectedIndex = 1;
                    break;
                case "5":
                    FlowerGeneR.SelectedIndex = 1;
                    FlowerGeneY.SelectedIndex = 1;
                    break;
                case "7":
                    FlowerGeneR.SelectedIndex = 2;
                    FlowerGeneY.SelectedIndex = 1;
                    break;
                case "C":
                    FlowerGeneR.SelectedIndex = 0;
                    FlowerGeneY.SelectedIndex = 2;
                    break;
                case "D":
                    FlowerGeneR.SelectedIndex = 1;
                    FlowerGeneY.SelectedIndex = 2;
                    break;
                case "F":
                    FlowerGeneR.SelectedIndex = 2;
                    FlowerGeneY.SelectedIndex = 2;
                    break;
                default:
                    FlowerGeneR.SelectedIndex = -1;
                    FlowerGeneY.SelectedIndex = -1;
                    break;
            }
        }

        private void GeneSelectionChangeCommitted(object sender, EventArgs e)
        {
            int R = FlowerGeneR.SelectedIndex;
            int Y = FlowerGeneY.SelectedIndex;
            int W = FlowerGeneW.SelectedIndex;
            int S = FlowerGeneS.SelectedIndex;

            setGeneTextBox(R, Y, W, S);
        }

        private void setGeneTextBox(int R, int Y, int W, int S)
        {
            string firstByte;
            switch (W)
            {
                case 0:
                    switch (S)
                    {
                        case 0:
                            firstByte = "0";
                            break;
                        case 1:
                            firstByte = "4";
                            break;
                        case 2:
                            firstByte = "C";
                            break;
                        default:
                            firstByte = "8";
                            break;
                    }
                    break;
                case 1:
                    switch (S)
                    {
                        case 0:
                            firstByte = "1";
                            break;
                        case 1:
                            firstByte = "5";
                            break;
                        case 2:
                            firstByte = "D";
                            break;
                        default:
                            firstByte = "9";
                            break;
                    }
                    break;
                case 2:
                    switch (S)
                    {
                        case 0:
                            firstByte = "3";
                            break;
                        case 1:
                            firstByte = "7";
                            break;
                        case 2:
                            firstByte = "F";
                            break;
                        default:
                            firstByte = "B";
                            break;
                    }
                    break;
                default:
                    switch (S)
                    {
                        case 0:
                            firstByte = "2";
                            break;
                        case 1:
                            firstByte = "6";
                            break;
                        case 2:
                            firstByte = "E";
                            break;
                        default:
                            firstByte = "A";
                            break;
                    }
                    break;
            }
            string secondByte;
            switch (R)
            {
                case 0:
                    switch (Y)
                    {
                        case 0:
                            secondByte = "0";
                            break;
                        case 1:
                            secondByte = "4";
                            break;
                        case 2:
                            secondByte = "C";
                            break;
                        default:
                            secondByte = "8";
                            break;
                    }
                    break;
                case 1:
                    switch (Y)
                    {
                        case 0:
                            secondByte = "1";
                            break;
                        case 1:
                            secondByte = "5";
                            break;
                        case 2:
                            secondByte = "D";
                            break;
                        default:
                            secondByte = "9";
                            break;
                    }
                    break;
                case 2:
                    switch (Y)
                    {
                        case 0:
                            secondByte = "3";
                            break;
                        case 1:
                            secondByte = "7";
                            break;
                        case 2:
                            secondByte = "F";
                            break;
                        default:
                            secondByte = "B";
                            break;
                    }
                    break;
                default:
                    switch (Y)
                    {
                        case 0:
                            secondByte = "2";
                            break;
                        case 1:
                            secondByte = "6";
                            break;
                        case 2:
                            secondByte = "E";
                            break;
                        default:
                            secondByte = "A";
                            break;
                    }
                    break;
            }
            //Debug.Print(firstByte + secondByte);
            if (HexModeButton.Tag.ToString() == "Normal")
            {
                HexModeButton_Click(null, null);
            }
            AmountOrCountTextbox.Text = AmountOrCountTextbox.Text.Substring(0, 6) + firstByte + secondByte;
            SelectedItem.setup(GetNameFromID(IDTextbox.Text, itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + AmountOrCountTextbox.Text, 16), GetImagePathFromID(IDTextbox.Text, itemSource, Convert.ToUInt32("0x" + AmountOrCountTextbox.Text, 16)), true);
            updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
        }

        #endregion

        private void setEatButton()
        {
            EatButton.Visible = true;
            Random rnd = new Random();
            int dice = rnd.Next(1, 8);

            switch (dice)
            {
                case 1:
                    EatButton.Text = "Eat 10 Apples";
                    break;
                case 2:
                    EatButton.Text = "Eat 10 Oranges";
                    break;
                case 3:
                    EatButton.Text = "Eat 10 Cherries";
                    break;
                case 4:
                    EatButton.Text = "Eat 10 Pears";
                    break;
                case 5:
                    EatButton.Text = "Eat 10 Peaches";
                    break;
                case 6:
                    EatButton.Text = "Eat 10 Coconuts";
                    break;
                default:
                    EatButton.Text = "Eat 10 Turnips";
                    break;
            }
        }

        private void readWeatherSeed()
        {
            byte[] b = Utilities.GetWeatherSeed(socket, usb);
            string result = Utilities.ByteToHexString(b);
            UInt32 decValue = Convert.ToUInt32(Utilities.flip(result), 16);
            UInt32 Seed = decValue - 2147483648;
            WeatherSeedTextbox.Text = Seed.ToString();
        }

        private void EatButton_Click(object sender, EventArgs e)
        {
            Utilities.setStamina(socket, usb, "0A");

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();

            setEatButton();
        }

        private void PoopButton_Click(object sender, EventArgs e)
        {
            Utilities.setStamina(socket, usb, "00");

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void UpdateTurnipPrices()
        {
            UInt64[] turnipPrices = Utilities.GetTurnipPrices(socket, usb);
            turnipBuyPrice.Clear();
            turnipBuyPrice.SelectionAlignment = HorizontalAlignment.Center;
            turnipBuyPrice.Text = String.Format("{0}", turnipPrices[12]);
            UInt64 buyPrice = UInt64.Parse(String.Format("{0}", turnipPrices[12]));

            turnipSell1AM.Clear();
            turnipSell1AM.Text = String.Format("{0}", turnipPrices[0]);
            UInt64 MondayAM = UInt64.Parse(String.Format("{0}", turnipPrices[0]));
            setTurnipColor(buyPrice, MondayAM, turnipSell1AM);

            turnipSell1PM.Clear();
            turnipSell1PM.Text = String.Format("{0}", turnipPrices[1]);
            UInt64 MondayPM = UInt64.Parse(String.Format("{0}", turnipPrices[1]));
            setTurnipColor(buyPrice, MondayPM, turnipSell1PM);

            turnipSell2AM.Clear();
            turnipSell2AM.Text = String.Format("{0}", turnipPrices[2]);
            UInt64 TuesdayAM = UInt64.Parse(String.Format("{0}", turnipPrices[2]));
            setTurnipColor(buyPrice, TuesdayAM, turnipSell2AM);

            turnipSell2PM.Clear();
            turnipSell2PM.Text = String.Format("{0}", turnipPrices[3]);
            UInt64 TuesdayPM = UInt64.Parse(String.Format("{0}", turnipPrices[3]));
            setTurnipColor(buyPrice, TuesdayPM, turnipSell2PM);

            turnipSell3AM.Clear();
            turnipSell3AM.Text = String.Format("{0}", turnipPrices[4]);
            UInt64 WednesdayAM = UInt64.Parse(String.Format("{0}", turnipPrices[4]));
            setTurnipColor(buyPrice, WednesdayAM, turnipSell3AM);

            turnipSell3PM.Clear();
            turnipSell3PM.Text = String.Format("{0}", turnipPrices[5]);
            UInt64 WednesdayPM = UInt64.Parse(String.Format("{0}", turnipPrices[5]));
            setTurnipColor(buyPrice, WednesdayPM, turnipSell3PM);

            turnipSell4AM.Clear();
            turnipSell4AM.Text = String.Format("{0}", turnipPrices[6]);
            UInt64 ThursdayAM = UInt64.Parse(String.Format("{0}", turnipPrices[6]));
            setTurnipColor(buyPrice, ThursdayAM, turnipSell4AM);

            turnipSell4PM.Clear();
            turnipSell4PM.Text = String.Format("{0}", turnipPrices[7]);
            UInt64 ThursdayPM = UInt64.Parse(String.Format("{0}", turnipPrices[7]));
            setTurnipColor(buyPrice, ThursdayPM, turnipSell4PM);

            turnipSell5AM.Clear();
            turnipSell5AM.Text = String.Format("{0}", turnipPrices[8]);
            UInt64 FridayAM = UInt64.Parse(String.Format("{0}", turnipPrices[8]));
            setTurnipColor(buyPrice, FridayAM, turnipSell5AM);

            turnipSell5PM.Clear();
            turnipSell5PM.Text = String.Format("{0}", turnipPrices[9]);
            UInt64 FridayPM = UInt64.Parse(String.Format("{0}", turnipPrices[9]));
            setTurnipColor(buyPrice, FridayPM, turnipSell5PM);

            turnipSell6AM.Clear();
            turnipSell6AM.Text = String.Format("{0}", turnipPrices[10]);
            UInt64 SaturdayAM = UInt64.Parse(String.Format("{0}", turnipPrices[10]));
            setTurnipColor(buyPrice, SaturdayAM, turnipSell6AM);

            turnipSell6PM.Clear();
            turnipSell6PM.Text = String.Format("{0}", turnipPrices[11]);
            UInt64 SaturdayPM = UInt64.Parse(String.Format("{0}", turnipPrices[11]));
            setTurnipColor(buyPrice, SaturdayPM, turnipSell6PM);

            UInt64[] price = { MondayAM, MondayPM, TuesdayAM, TuesdayPM, WednesdayAM, WednesdayPM, ThursdayAM, ThursdayPM, FridayAM, FridayPM, SaturdayAM, SaturdayPM };
            UInt64 highest = findHighest(price);
            setStar(highest, MondayAM, MondayPM, TuesdayAM, TuesdayPM, WednesdayAM, WednesdayPM, ThursdayAM, ThursdayPM, FridayAM, FridayPM, SaturdayAM, SaturdayPM);
        }
        private void setTurnipColor(UInt64 buyPrice, UInt64 comparePrice, RichTextBox target)
        {
            target.SelectionAlignment = HorizontalAlignment.Center;

            if (comparePrice > buyPrice)
            {
                target.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            }
            else if (comparePrice < buyPrice)
            {
                target.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(255)))), ((int)(((byte)(0)))));
            }
        }

        private UInt64 findHighest(UInt64[] price)
        {
            UInt64 highest = 0;
            for (int i = 0; i < price.Length; i++)
            {
                if (price[i] > highest)
                {
                    highest = price[i];
                }
            }
            return highest;
        }

        private void setStar(UInt64 highest, UInt64 MondayAM, UInt64 MondayPM, UInt64 TuesdayAM, UInt64 TuesdayPM, UInt64 WednesdayAM, UInt64 WednesdayPM, UInt64 ThursdayAM, UInt64 ThursdayPM, UInt64 FridayAM, UInt64 FridayPM, UInt64 SaturdayAM, UInt64 SaturdayPM)
        {
            if (MondayAM >= highest) { mondayAMStar.Visible = true; } else { mondayAMStar.Visible = false; };
            if (MondayPM >= highest) { mondayPMStar.Visible = true; } else { mondayPMStar.Visible = false; };

            if (TuesdayAM >= highest) { tuesdayAMStar.Visible = true; } else { tuesdayAMStar.Visible = false; };
            if (TuesdayPM >= highest) { tuesdayPMStar.Visible = true; } else { tuesdayPMStar.Visible = false; };

            if (WednesdayAM >= highest) { wednesdayAMStar.Visible = true; } else { wednesdayAMStar.Visible = false; };
            if (WednesdayPM >= highest) { wednesdayPMStar.Visible = true; } else { wednesdayPMStar.Visible = false; };

            if (ThursdayAM >= highest) { thursdayAMStar.Visible = true; } else { thursdayAMStar.Visible = false; };
            if (ThursdayPM >= highest) { thursdayPMStar.Visible = true; } else { thursdayPMStar.Visible = false; };

            if (FridayAM >= highest) { fridayAMStar.Visible = true; } else { fridayAMStar.Visible = false; };
            if (FridayPM >= highest) { fridayPMStar.Visible = true; } else { fridayPMStar.Visible = false; };

            if (SaturdayAM >= highest) { saturdayAMStar.Visible = true; } else { saturdayAMStar.Visible = false; };
            if (SaturdayPM >= highest) { saturdayPMStar.Visible = true; } else { saturdayPMStar.Visible = false; };
        }

        private void loadReaction(int player = 0)
        {
            byte[] reactionBank = Utilities.getReaction(socket, usb, player);
            //Debug.Print(Encoding.ASCII.GetString(reactionBank));

            byte[] reactions1 = new byte[1];
            byte[] reactions2 = new byte[1];
            byte[] reactions3 = new byte[1];
            byte[] reactions4 = new byte[1];
            byte[] reactions5 = new byte[1];
            byte[] reactions6 = new byte[1];
            byte[] reactions7 = new byte[1];
            byte[] reactions8 = new byte[1];

            Buffer.BlockCopy(reactionBank, 0, reactions1, 0x0, 0x1);
            Buffer.BlockCopy(reactionBank, 1, reactions2, 0x0, 0x1);
            Buffer.BlockCopy(reactionBank, 2, reactions3, 0x0, 0x1);
            Buffer.BlockCopy(reactionBank, 3, reactions4, 0x0, 0x1);
            Buffer.BlockCopy(reactionBank, 4, reactions5, 0x0, 0x1);
            Buffer.BlockCopy(reactionBank, 5, reactions6, 0x0, 0x1);
            Buffer.BlockCopy(reactionBank, 6, reactions7, 0x0, 0x1);
            Buffer.BlockCopy(reactionBank, 7, reactions8, 0x0, 0x1);

            setReactionBox(Utilities.ByteToHexString(reactions1), ReactionSlot1);
            setReactionBox(Utilities.ByteToHexString(reactions2), ReactionSlot2);
            setReactionBox(Utilities.ByteToHexString(reactions3), ReactionSlot3);
            setReactionBox(Utilities.ByteToHexString(reactions4), ReactionSlot4);
            setReactionBox(Utilities.ByteToHexString(reactions5), ReactionSlot5);
            setReactionBox(Utilities.ByteToHexString(reactions6), ReactionSlot6);
            setReactionBox(Utilities.ByteToHexString(reactions7), ReactionSlot7);
            setReactionBox(Utilities.ByteToHexString(reactions8), ReactionSlot8);
        }

        private void setReactionBox(string reaction, ComboBox box)
        {
            if (reaction == "00")
            {
                box.SelectedIndex = -1;
                return;
            }
            string hexValue = reaction;
            int decValue = Convert.ToInt32(hexValue, 16) - 1;
            if (decValue >= 118)
                box.SelectedIndex = -1;
            else
                box.SelectedIndex = decValue;
        }

        private void PlayerInventorySelectorOther_SelectedIndexChanged(object sender, EventArgs e)
        {
            loadReaction(PlayerInventorySelectorOther.SelectedIndex);
        }

        private void SetReactionWheelButton_Click(object sender, EventArgs e)
        {
            int player = PlayerInventorySelectorOther.SelectedIndex;

            string reactionFirstHalf = (Utilities.precedingZeros((ReactionSlot1.SelectedIndex + 1).ToString("X"), 2) + Utilities.precedingZeros((ReactionSlot2.SelectedIndex + 1).ToString("X"), 2) + Utilities.precedingZeros((ReactionSlot3.SelectedIndex + 1).ToString("X"), 2) + Utilities.precedingZeros((ReactionSlot4.SelectedIndex + 1).ToString("X"), 2));
            string reactionSecondHalf = (Utilities.precedingZeros((ReactionSlot5.SelectedIndex + 1).ToString("X"), 2) + Utilities.precedingZeros((ReactionSlot6.SelectedIndex + 1).ToString("X"), 2) + Utilities.precedingZeros((ReactionSlot7.SelectedIndex + 1).ToString("X"), 2) + Utilities.precedingZeros((ReactionSlot8.SelectedIndex + 1).ToString("X"), 2));
            Utilities.setReaction(socket, usb, player, reactionFirstHalf, reactionSecondHalf);
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void maxSpeedX1Btn_Click(object sender, EventArgs e)
        {
            maxSpeedX1Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            maxSpeedX2Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX3Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX5Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX100Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            Utilities.pokeAddress(socket, usb, Utilities.MaxSpeedAddress.ToString("X"), Utilities.MaxSpeedX1);
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void maxSpeedX2Btn_Click(object sender, EventArgs e)
        {
            maxSpeedX1Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX2Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            maxSpeedX3Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX5Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX100Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            Utilities.pokeAddress(socket, usb, Utilities.MaxSpeedAddress.ToString("X"), Utilities.MaxSpeedX2);
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void maxSpeedX3Btn_Click(object sender, EventArgs e)
        {
            maxSpeedX1Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX2Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX3Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            maxSpeedX5Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX100Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            Utilities.pokeAddress(socket, usb, Utilities.MaxSpeedAddress.ToString("X"), Utilities.MaxSpeedX3);
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void maxSpeedX5Btn_Click(object sender, EventArgs e)
        {
            maxSpeedX1Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX2Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX3Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX5Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            maxSpeedX100Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            Utilities.pokeAddress(socket, usb, Utilities.MaxSpeedAddress.ToString("X"), Utilities.MaxSpeedX5);
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void maxSpeedX100Btn_Click(object sender, EventArgs e)
        {
            maxSpeedX1Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX2Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX3Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX5Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            maxSpeedX100Btn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));

            Utilities.pokeAddress(socket, usb, Utilities.MaxSpeedAddress.ToString("X"), Utilities.MaxSpeedX100);
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void DisableCollisionToggle_CheckedChanged(object sender, EventArgs e)
        {
            if (DisableCollisionToggle.Checked)
            {
                Utilities.pokeMainAddress(socket, usb, Utilities.CollisionAddress.ToString("X"), Utilities.CollisionDisable);
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
            else
            {
                Utilities.pokeMainAddress(socket, usb, Utilities.CollisionAddress.ToString("X"), Utilities.CollisionEnable);
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
        }

        private void FastSwimToggle_CheckedChanged(object sender, EventArgs e)
        {
            if (FastSwimToggle.Checked)
            {
                Utilities.SetFastSwimSpeed(socket, usb, true);
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
            else
            {
                Utilities.SetFastSwimSpeed(socket, usb, false);
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
        }

        private void FreezeTimeButton_Click(object sender, EventArgs e)
        {
            FreezeTimeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            UnFreezeTimeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            Utilities.pokeMainAddress(socket, usb, Utilities.freezeTimeAddress.ToString("X"), Utilities.freezeTimeValue);
            readtime();
            DateAndTimeControlPanel.Visible = true;

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void readtime()
        {
            byte[] b = Utilities.peekAddress(socket, usb, Utilities.readTimeAddress, 6);
            string time = Utilities.ByteToHexString(b);

            Debug.Print(time);

            Int32 year = Convert.ToInt32(Utilities.flip(time.Substring(0, 4)), 16);
            Int32 month = Convert.ToInt32((time.Substring(4, 2)), 16);
            Int32 day = Convert.ToInt32((time.Substring(6, 2)), 16);
            Int32 hour = Convert.ToInt32((time.Substring(8, 2)), 16);
            Int32 min = Convert.ToInt32((time.Substring(10, 2)), 16);

            if (year > 3000 || month > 12 || day > 31 || hour > 24 || min > 60) //Try for Chineses
            {
                b = Utilities.peekAddress(socket, usb, Utilities.readTimeAddress + Utilities.ChineseLanguageOffset, 6);
                time = Utilities.ByteToHexString(b);

                year = Convert.ToInt32(Utilities.flip(time.Substring(0, 4)), 16);
                month = Convert.ToInt32((time.Substring(4, 2)), 16);
                day = Convert.ToInt32((time.Substring(6, 2)), 16);
                hour = Convert.ToInt32((time.Substring(8, 2)), 16);
                min = Convert.ToInt32((time.Substring(10, 2)), 16);

                if (!(year > 3000 || month > 12 || day > 31 || hour > 24 || min > 60))
                    ChineseFlag = true;
            }

            YearTextbox.Clear();
            YearTextbox.SelectionAlignment = HorizontalAlignment.Center;
            YearTextbox.Text = year.ToString();

            MonthTextbox.Clear();
            MonthTextbox.SelectionAlignment = HorizontalAlignment.Center;
            MonthTextbox.Text = month.ToString();

            DayTextbox.Clear();
            DayTextbox.SelectionAlignment = HorizontalAlignment.Center;
            DayTextbox.Text = day.ToString();

            HourTextbox.Clear();
            HourTextbox.SelectionAlignment = HorizontalAlignment.Center;
            HourTextbox.Text = hour.ToString();

            MinuteTextbox.Clear();
            MinuteTextbox.SelectionAlignment = HorizontalAlignment.Center;
            MinuteTextbox.Text = min.ToString();
        }

        private void UnFreezeTimeButton_Click(object sender, EventArgs e)
        {
            UnFreezeTimeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            FreezeTimeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            Utilities.pokeMainAddress(socket, usb, Utilities.freezeTimeAddress.ToString("X"), Utilities.unfreezeTimeValue);
            DateAndTimeControlPanel.Visible = false;
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void animationSpeedx50_Click(object sender, EventArgs e)
        {
            animationSpeedx1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx0_1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx50.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            animationSpeedx5.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            Utilities.pokeMainAddress(socket, usb, Utilities.aSpeedAddress.ToString("X"), Utilities.aSpeedX50);
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void animationSpeedx5_Click(object sender, EventArgs e)
        {
            animationSpeedx1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx0_1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx50.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx5.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));

            Utilities.pokeMainAddress(socket, usb, Utilities.aSpeedAddress.ToString("X"), Utilities.aSpeedX5);
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void animationSpeedx2_Click(object sender, EventArgs e)
        {
            animationSpeedx1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            animationSpeedx0_1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx50.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx5.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            Utilities.pokeMainAddress(socket, usb, Utilities.aSpeedAddress.ToString("X"), Utilities.aSpeedX2);
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void animationSpeedx0_1_Click(object sender, EventArgs e)
        {
            animationSpeedx1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx0_1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            animationSpeedx50.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx5.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            Utilities.pokeMainAddress(socket, usb, Utilities.aSpeedAddress.ToString("X"), Utilities.aSpeedX01);
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void animationSpeedx1_Click(object sender, EventArgs e)
        {
            animationSpeedx1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            animationSpeedx2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx0_1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx50.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            animationSpeedx5.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            Utilities.pokeMainAddress(socket, usb, Utilities.aSpeedAddress.ToString("X"), Utilities.aSpeedX1);
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void SetDateAndTimeButton_Click(object sender, EventArgs e)
        {
            int decYear = int.Parse(YearTextbox.Text);
            if (decYear > 2060)
            {
                decYear = 2060;
            }
            else if (decYear < 1970)
            {
                decYear = 1970;
            }
            YearTextbox.Text = decYear.ToString();
            string hexYear = decYear.ToString("X");

            int decMonth = int.Parse(MonthTextbox.Text);
            if (decMonth > 12)
            {
                decMonth = 12;
            }
            else if (decMonth < 0)
            {
                decMonth = 1;
            }
            MonthTextbox.Text = decMonth.ToString();
            string hexMonth = decMonth.ToString("X");

            int decDay = int.Parse(DayTextbox.Text);
            if (decDay > 31)
            {
                decDay = 31;
            }
            else if (decDay < 0)
            {
                decDay = 1;
            }
            DayTextbox.Text = decDay.ToString();
            string hexDay = decDay.ToString("X");

            int decHour = int.Parse(HourTextbox.Text);
            if (decHour > 23)
            {
                decHour = 23;
            }
            else if (decHour < 0)
            {
                decHour = 0;
            }
            HourTextbox.Text = decHour.ToString();
            string hexHour = decHour.ToString("X");


            int decMin = int.Parse(MinuteTextbox.Text);
            if (decMin > 59)
            {
                decMin = 59;
            }
            else if (decMin < 0)
            {
                decMin = 0;
            }
            MinuteTextbox.Text = decMin.ToString();
            string hexMin = decMin.ToString("X");

            if (ChineseFlag)
            {
                Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + Utilities.ChineseLanguageOffset).ToString("X"), Utilities.flip(Utilities.precedingZeros(hexYear, 4)));
                Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x2 + Utilities.ChineseLanguageOffset).ToString("X"), Utilities.precedingZeros(hexMonth, 2) + Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2) + Utilities.precedingZeros(hexMin, 2));
            }
            else
            {
                Utilities.pokeAddress(socket, usb, Utilities.readTimeAddress.ToString("X"), Utilities.flip(Utilities.precedingZeros(hexYear, 4)));
                Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x2).ToString("X"), Utilities.precedingZeros(hexMonth, 2) + Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2) + Utilities.precedingZeros(hexMin, 2));
            }

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void Add1HourButton_Click(object sender, EventArgs e)
        {
            int decHour = int.Parse(HourTextbox.Text) + 1;
            if (decHour >= 24)
            {
                decHour = 0;
                string hexHour = decHour.ToString("X");

                int decDay = int.Parse(DayTextbox.Text) + 1;
                if (decDay > 31)
                {
                    decDay = 1;
                    string hexDay = decDay.ToString("X");

                    int decMonth = int.Parse(MonthTextbox.Text) + 1;
                    if (decMonth > 12)
                    {
                        decMonth = 1;
                        string hexMonth = decMonth.ToString("X");

                        int decYear = int.Parse(YearTextbox.Text) + 1;
                        string hexYear = decYear.ToString("X");

                        if (ChineseFlag)
                            Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + Utilities.ChineseLanguageOffset).ToString("X"), Utilities.precedingZeros(hexYear, 4) + Utilities.precedingZeros(hexMonth, 2) + Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2));
                        else
                            Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress).ToString("X"), Utilities.precedingZeros(hexYear, 4) + Utilities.precedingZeros(hexMonth, 2) + Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2));
                    }
                    else
                    {
                        string hexMonth = decMonth.ToString("X");
                        if (ChineseFlag)
                            Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x2 + Utilities.ChineseLanguageOffset).ToString("X"), Utilities.precedingZeros(hexMonth, 2) + Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2));
                        else
                            Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x2).ToString("X"), Utilities.precedingZeros(hexMonth, 2) + Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2));
                    }
                }
                else
                {
                    string hexDay = decDay.ToString("X");
                    if (ChineseFlag)
                        Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x3 + Utilities.ChineseLanguageOffset).ToString("X"), Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2));
                    else
                        Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x3).ToString("X"), Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2));
                }
            }
            else
            {
                string hexHour = decHour.ToString("X");
                if (ChineseFlag)
                    Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x4 + Utilities.ChineseLanguageOffset).ToString("X"), Utilities.precedingZeros(hexHour, 2));
                else
                    Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x4).ToString("X"), Utilities.precedingZeros(hexHour, 2));
            }
            readtime();
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void Minus1HourButton_Click(object sender, EventArgs e)
        {
            int decHour = int.Parse(HourTextbox.Text) - 1;
            if (decHour < 0)
            {
                decHour = 23;
                string hexHour = decHour.ToString("X");

                int decDay = int.Parse(DayTextbox.Text) - 1;
                if (decDay < 1)
                {
                    decDay = 28;
                    string hexDay = decDay.ToString("X");

                    int decMonth = int.Parse(MonthTextbox.Text) + 1;
                    if (decMonth < 1)
                    {
                        decMonth = 12;
                        string hexMonth = decMonth.ToString("X");

                        int decYear = int.Parse(YearTextbox.Text) + 1;
                        string hexYear = decYear.ToString("X");

                        if (ChineseFlag)
                            Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + Utilities.ChineseLanguageOffset).ToString("X"), Utilities.precedingZeros(hexYear, 4) + Utilities.precedingZeros(hexMonth, 2) + Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2));
                        else
                            Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress).ToString("X"), Utilities.precedingZeros(hexYear, 4) + Utilities.precedingZeros(hexMonth, 2) + Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2));
                    }
                    else
                    {
                        string hexMonth = decMonth.ToString("X");
                        if (ChineseFlag)
                            Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x2 + Utilities.ChineseLanguageOffset).ToString("X"), Utilities.precedingZeros(hexMonth, 2) + Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2));
                        else
                            Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x2).ToString("X"), Utilities.precedingZeros(hexMonth, 2) + Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2));
                    }
                }
                else
                {
                    string hexDay = decDay.ToString("X");
                    if (ChineseFlag)
                        Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x3 + Utilities.ChineseLanguageOffset).ToString("X"), Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2));
                    else
                        Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x3).ToString("X"), Utilities.precedingZeros(hexDay, 2) + Utilities.precedingZeros(hexHour, 2));
                }
            }
            else
            {
                string hexHour = decHour.ToString("X");
                if (ChineseFlag)
                    Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x4 + Utilities.ChineseLanguageOffset).ToString("X"), Utilities.precedingZeros(hexHour, 2));
                else
                    Utilities.pokeAddress(socket, usb, (Utilities.readTimeAddress + 0x4).ToString("X"), Utilities.precedingZeros(hexHour, 2));
            }
            readtime();
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void SetTurnipPriceMaxButton_Click(object sender, EventArgs e)
        {
            string max = "999999999";
            string min = "1";
            DialogResult dialogResult = MyMessageBox.Show("Are you sure you want to set all the turnip prices to MAX?\n[Warning] All original prices will be overwritten!", "Set all turnip prices", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
            if (dialogResult == DialogResult.Yes)
            {
                UInt32[] prices = new UInt32[13] {
                Convert.ToUInt32(max, 10), Convert.ToUInt32(max, 10),
                Convert.ToUInt32(max, 10), Convert.ToUInt32(max, 10),
                Convert.ToUInt32(max, 10), Convert.ToUInt32(max, 10),
                Convert.ToUInt32(max, 10), Convert.ToUInt32(max, 10),
                Convert.ToUInt32(max, 10), Convert.ToUInt32(max, 10),
                Convert.ToUInt32(max, 10), Convert.ToUInt32(max, 10),
                Convert.ToUInt32(min, 10)};

                try
                {
                    Utilities.ChangeTurnipPrices(socket, usb, prices);
                    UpdateTurnipPrices();
                }
                catch (Exception ex)
                {
                    MyLog.logEvent("MainForm", "SetAllTurnip: " + ex.Message.ToString());
                    MyMessageBox.Show(ex.Message.ToString(), "This is a terrible way of doing this!");
                }

                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
        }

        private void SetTurnipPriceButton_Click(object sender, EventArgs e)
        {
            if (turnipBuyPrice.Text == "" ||
                turnipSell1AM.Text == "" || turnipSell1PM.Text == "" ||
                turnipSell2AM.Text == "" || turnipSell2PM.Text == "" ||
                turnipSell3AM.Text == "" || turnipSell3PM.Text == "" ||
                turnipSell4AM.Text == "" || turnipSell4PM.Text == "" ||
                turnipSell5AM.Text == "" || turnipSell5PM.Text == "" ||
                turnipSell6AM.Text == "" || turnipSell6PM.Text == "")
            {
                MessageBox.Show("Turnip prices cannot be empty");
                return;
            }

            DialogResult dialogResult = MyMessageBox.Show("Are you sure you want to set the turnip prices?\n[Warning] All original prices will be overwritten!", "Set turnip prices", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);
            if (dialogResult == DialogResult.Yes)
            {
                UInt32[] prices = new UInt32[13] {
                Convert.ToUInt32(turnipSell1AM.Text, 10), Convert.ToUInt32(turnipSell1PM.Text, 10),
                Convert.ToUInt32(turnipSell2AM.Text, 10), Convert.ToUInt32(turnipSell2PM.Text, 10),
                Convert.ToUInt32(turnipSell3AM.Text, 10), Convert.ToUInt32(turnipSell3PM.Text, 10),
                Convert.ToUInt32(turnipSell4AM.Text, 10), Convert.ToUInt32(turnipSell4PM.Text, 10),
                Convert.ToUInt32(turnipSell5AM.Text, 10), Convert.ToUInt32(turnipSell5PM.Text, 10),
                Convert.ToUInt32(turnipSell6AM.Text, 10), Convert.ToUInt32(turnipSell6PM.Text, 10),
                Convert.ToUInt32(turnipBuyPrice.Text, 10)};

                try
                {
                    Utilities.ChangeTurnipPrices(socket, usb, prices);
                    UpdateTurnipPrices();
                }
                catch (Exception ex)
                {
                    MyLog.logEvent("MainForm", "SetTurnip: " + ex.Message.ToString());
                    MyMessageBox.Show(ex.Message.ToString(), "This is a terrible way of doing this!");
                }

                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
        }

        private void LoadGridView(byte[] source, DataGridView grid, ref int[] rate, int size, int num, int mode = 0)
        {
            if (source != null)
            {
                grid.DataSource = null;
                grid.Rows.Clear();
                grid.Columns.Clear();

                DataTable dt = new DataTable();

                grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                grid.DefaultCellStyle.BackColor = Color.FromArgb(255, 47, 49, 54);
                grid.DefaultCellStyle.ForeColor = Color.White;
                //grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 57, 60, 67);


                grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(255, 57, 60, 67);
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 57, 60, 67);
                grid.EnableHeadersVisualStyles = false;

                DataGridViewCellStyle btnStyle = new DataGridViewCellStyle
                {
                    BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218))))),
                    Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                };

                DataGridViewCellStyle selectedbtnStyle = new DataGridViewCellStyle
                {
                    BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255))))),
                    Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                };

                DataGridViewCellStyle FontStyle = new DataGridViewCellStyle
                {
                    Font = new System.Drawing.Font("Arial", 10F, System.Drawing.FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleLeft
                };

                dt.Columns.Add("Index", typeof(int));
                dt.Columns.Add("Name", typeof(string));
                dt.Columns.Add("ID", typeof(string));
                dt.Columns.Add(" ", typeof(int));


                UInt16 Id;
                string Name;

                for (int i = 0; i < num / 2; i++)
                {
                    Id = (UInt16)(source[i * size * 2]
                             + (source[i * size * 2 + 1] << 8));
                    Name = GetNameFromID(String.Format("{0:X4}", Id), itemSource);
                    int spawnRate;
                    if (grid == InsectGridView)
                    {
                        spawnRate = getSpawnRate(source, i * size * 2 + 2, size - 12);
                    }
                    else if (grid == SeaCreatureGridView)
                    {
                        spawnRate = getSpawnRate(source, i * size * 2 + 2, size - 10);
                    }
                    else
                    {
                        spawnRate = getSpawnRate(source, i * size * 2 + 2, size - 8);
                    }
                    dt.Rows.Add(new object[] { i, Name, String.Format("{0:X4}", Id), spawnRate });
                }
                grid.DataSource = dt;


                DataGridViewImageColumn imageColumn = new DataGridViewImageColumn
                {
                    Name = "Image",
                    HeaderText = "Image",
                    ImageLayout = DataGridViewImageCellLayout.Zoom,
                    Resizable = DataGridViewTriState.False,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                };
                grid.Columns.Insert(0, imageColumn);

                // Index
                grid.Columns[1].DefaultCellStyle = FontStyle;
                grid.Columns[1].Width = 50;
                //grid.Columns[1].Visible = false;
                grid.Columns[1].Resizable = DataGridViewTriState.False;

                // Name
                grid.Columns[2].DefaultCellStyle = FontStyle;
                grid.Columns[2].Width = 250;
                grid.Columns[2].Resizable = DataGridViewTriState.False;

                // ID
                grid.Columns[3].DefaultCellStyle = FontStyle;
                //grid.Columns[3].Visible = false;
                grid.Columns[3].Width = 60;
                grid.Columns[3].Resizable = DataGridViewTriState.False;

                // Rate
                grid.Columns[4].DefaultCellStyle = FontStyle;
                grid.Columns[4].Width = 50;
                grid.Columns[4].Visible = false;
                //grid.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                grid.Columns[4].Resizable = DataGridViewTriState.False;

                DataGridViewProgressColumn barColumn = new DataGridViewProgressColumn
                {
                    Name = "Bar",
                    HeaderText = "",
                    DefaultCellStyle = FontStyle,
                    Width = 320,
                    Resizable = DataGridViewTriState.False,
                };
                grid.Columns.Add(barColumn);

                DataGridViewButtonColumn minColumn = new DataGridViewButtonColumn
                {
                    Name = "Min",
                    HeaderText = "",
                    FlatStyle = FlatStyle.Popup,
                    DefaultCellStyle = btnStyle,
                    Width = 100,
                    Text = "Disable Spawn",
                    UseColumnTextForButtonValue = true,
                    Resizable = DataGridViewTriState.False,
                };
                grid.Columns.Add(minColumn);

                DataGridViewTextBoxColumn separator1 = new DataGridViewTextBoxColumn
                {
                    Width = 10,
                    Resizable = DataGridViewTriState.False,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                };
                grid.Columns.Add(separator1);

                DataGridViewButtonColumn defaultColumn = new DataGridViewButtonColumn
                {
                    Name = "Default",
                    HeaderText = "",
                    FlatStyle = FlatStyle.Popup,
                    DefaultCellStyle = selectedbtnStyle,
                    Width = 100,
                    Text = "Default",
                    UseColumnTextForButtonValue = true,
                    Resizable = DataGridViewTriState.False,
                };
                grid.Columns.Add(defaultColumn);

                DataGridViewTextBoxColumn separator2 = new DataGridViewTextBoxColumn
                {
                    Width = 10,
                    Resizable = DataGridViewTriState.False,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                };
                grid.Columns.Add(separator2);

                DataGridViewButtonColumn maxColumn = new DataGridViewButtonColumn
                {
                    Name = "Max",
                    HeaderText = "",
                    FlatStyle = FlatStyle.Popup,
                    DefaultCellStyle = btnStyle,
                    Width = 100,
                    Text = "Max Spawn",
                    UseColumnTextForButtonValue = true,
                    Resizable = DataGridViewTriState.False,
                };
                grid.Columns.Add(maxColumn);

                rate = new int[num / 2];

                for (int i = 0; i < num / 2; i++)
                {
                    Id = (UInt16)(source[i * size * 2]
                            + (source[i * size * 2 + 1] << 8));

                    int spawnRate;
                    if (grid == InsectGridView)
                    {
                        spawnRate = getSpawnRate(source, i * size * 2 + 2, size - 12);
                    }
                    else if (grid == SeaCreatureGridView)
                    {
                        spawnRate = getSpawnRate(source, i * size * 2 + 2, size - 10);
                    }
                    else
                    {
                        spawnRate = getSpawnRate(source, i * size * 2 + 2, size - 8);
                    }

                    rate[i] = spawnRate;

                    DataGridViewProgressCell pc = new DataGridViewProgressCell
                    {
                        setValue = spawnRate,
                        remark = getRemark(String.Format("{0:X4}", Id)),
                        mode = mode,
                    };
                    grid.Rows[i].Cells[5] = pc;
                }

                grid.ColumnHeadersVisible = false;
                grid.ClearSelection();
            }
        }

        private int getSpawnRate(byte[] source, int index, int size)
        {
            int max = 0;
            for (int i = 0; i < size; i++)
            {
                if (source[index + i] > max)
                    max = source[index + i];
            }
            return max;
        }

        private string getRemark(string ID)
        {
            switch (ID)
            {
                case ("0272"): //Common butterfly
                    return ("Except on rainy days");
                case ("0271"): //Yellow butterfly
                    return ("Except on rainy days");
                case ("0247"): //Tiger butterfly
                    return ("Except on rainy days");
                case ("0262"): //Peacock butterfly
                    return ("Catch 20 or more bugs to spawn\nExcept on rainy days");
                case ("0D95"): //Common bluebottle
                    return ("Except on rainy days");
                case ("0D96"): //Paper kite butterfly
                    return ("Except on rainy days");
                case ("0D97"): //Great purple emperor
                    return ("Catch 50 or more bugs to spawn\nExcept on rainy days");
                case ("027C"): //Monarch butterfly
                    return ("Except on rainy days");
                case ("0273"): //Emperor butterfly
                    return ("Catch 20 or more bugs to spawn\nExcept on rainy days");
                case ("026C"): //Agrias butterfly
                    return ("Catch 20 or more bugs to spawn\nExcept on rainy days");
                case ("0248"): //Rajah brooke's birdwing
                    return ("Catch 20 or more bugs to spawn\nExcept on rainy days");
                case ("024A"): //Queen Alexandra's birdwing
                    return ("Catch 50 or more bugs to spawn\nExcept on rainy days");
                case ("0250"): //Moth
                    return ("Except on rainy days");
                case ("028C"): //Atlas moth
                    return ("Catch 20 or more bugs to spawn");
                case ("0D9C"): //Madagascan sunset moth
                    return ("Catch 20 or more bugs to spawn\nExcept on rainy days");

                case ("0284"): //Long locust
                    return ("");
                case ("0288"): //Migratory Locust
                    return ("Catch 20 or more bugs to spawn");
                case ("025D"): //Rice grasshopper
                    return ("");
                case ("0265"): //Grasshopper
                    return ("Except on rainy days");

                case ("0269"): //Cricket
                    return ("Except on rainy days");
                case ("0282"): //Bell cricket
                    return ("Except on rainy days");

                case ("025F"): //Mantis
                    return ("Except on rainy days");
                case ("0256"): //Orchid mantis
                    return ("Catch 20 or more bugs to spawn\nExcept on rainy days");

                case ("026F"): //Honeybee
                    return ("Except on rainy days");
                case ("0283"): //Wasp
                    return ("✶ Spawn rate seems to have no effect\nSpawn when nest falls from tree");

                case ("0246"): //Brown cicada
                    return ("");
                case ("026D"): //Robust cicada
                    return ("");
                case ("026A"): //Giant cicada
                    return ("Catch 20 or more bugs to spawn");
                case ("0289"): //Walker cicada
                    return ("");
                case ("0259"): //Evening cicada
                    return ("");

                case ("0281"): //Cicada shell
                    return ("Catch 50 or more bugs to spawn");

                case ("0249"): //Red dragonfly
                    return ("Except on rainy days");
                case ("0253"): //Darner dragonfly
                    return ("Except on rainy days");
                case ("027B"): //Banded dragonfly
                    return ("Catch 50 or more bugs to spawn\nExcept on rainy days");
                case ("14DB"): //Damselfly
                    return ("Except on rainy days");

                case ("025B"): //Firefly
                    return ("Except on rainy days");

                case ("027A"): //Mole cricket
                    return ("");

                case ("024B"): //Pondskater
                    return ("");
                case ("0252"): //Diving beetle
                    return ("");
                case ("1425"): //Giant water bug
                    return ("Catch 50 or more bugs to spawn");

                case ("0260"): //Stinkbug
                    return ("Except on rainy days");
                case ("0D9B"): //Man-faced stink bug
                    return ("Catch 20 or more bugs to spawn\nExcept on rainy days");

                case ("0287"): //Ladybug
                    return ("Except on rainy days");

                case ("0257"): //Tiger beetle
                    return ("Catch 20 or more bugs to spawn\nExcept on rainy days");
                case ("0285"): //Jewel beetle
                    return ("Catch 20 or more bugs to spawn\nExcept on rainy days");
                case ("028A"): //Violin beetle
                    return ("Except on rainy days");
                case ("0261"): //Citrus long-horned beetle
                    return ("Except on rainy days");
                case ("0D9F"): //Rosalia batesi beetle
                    return ("Catch 20 or more bugs to spawn\nExcept on rainy days");
                case ("0D9D"): //Blue weevil beetle
                    return ("");
                case ("025C"): //Dung beetle
                    return ("Spawn when there is snowball on the ground");
                case ("0266"): //Earth-boring dung beetle
                    return ("");
                case ("027F"): //Scarab beetle
                    return ("Catch 50 or more bugs to spawn");
                case ("0D98"): //Drone beetle
                    return ("");
                case ("0254"): //Goliath beetle
                    return ("Catch 100 or more bugs to spawn");

                case ("0278"): //Saw stag
                    return ("");
                case ("0270"): //Miyama stag
                    return ("");
                case ("027D"): //Giant stag
                    return ("Catch 50 or more bugs to spawn");
                case ("0277"): //Rainbow stag
                    return ("Catch 50 or more bugs to spawn");
                case ("025A"): //Cyclommatus stag
                    return ("Catch 100 or more bugs to spawn");
                case ("027E"): //Golden stag
                    return ("Catch 100 or more bugs to spawn");
                case ("0D9A"): //Giraffe stag
                    return ("Catch 100 or more bugs to spawn");

                case ("0264"): //Horned dynastid
                    return ("");
                case ("0267"): //Horned atlas
                    return ("Catch 100 or more bugs to spawn");
                case ("028D"): //Horned elephant
                    return ("Catch 100 or more bugs to spawn");
                case ("0258"): //Horned hercules
                    return ("Catch 100 or more bugs to spawn");

                case ("0276"): //Walking stick
                    return ("Catch 20 or more bugs to spawn");
                case ("0268"): //Walking leaf
                    return ("Catch 20 or more bugs to spawn");
                case ("026E"): //Bagworm
                    return ("✶ Spawn rate seems to have no effect\nSpawn when shaking tree");
                case ("024C"): //Ant
                    return ("✶ Spawn rate seems to have no effect\nSpawn when there is rotten turnip");
                case ("028B"): //Hermit crab
                    return ("Spawn on beach");
                case ("024F"): //Wharf roach
                    return ("Spawn on rocky formations at beach");
                case ("0255"): //Fly
                    return ("✶ Spawn rate seems to have no effect\nSpawn when there is trash item");
                case ("025E"): //Mosquito
                    return ("Except on rainy days");
                case ("0279"): //Flea
                    return ("Spawn on villagers");
                case ("0263"): //Snail
                    return ("Rainy days only");
                case ("024E"): //Pill bug
                    return ("Spawn underneath rocks");
                case ("0274"): //Centipede
                    return ("Spawn underneath rocks");
                case ("026B"): //Spider
                    return ("✶ Spawn rate seems to have no effect\nSpawn when shaking tree");

                case ("0286"): //Tarantula
                    return ("");
                case ("0280"): //Scorpion
                    return ("");

                case ("0DD3"): //Snowflake
                    return ("Spawn when in season/time");
                case ("16E3"): //Cherry-blossom petal
                    return ("Spawn when in season/time");
                case ("1CCE"): //Maple leaf
                    return ("Spawn when in season/time");

                case ("08AC"): //Koi
                    return ("Catch 20 or more fishes to spawn");
                case ("1486"): //Ranchu Goldfish
                    return ("Catch 20 or more fishes to spawn");
                case ("08B0"): //Soft-shelled Turtle
                    return ("Catch 20 or more fishes to spawn");
                case ("08B7"): //Giant Snakehead
                    return ("Catch 50 or more fishes to spawn");
                case ("08BB"): //Pike
                    return ("Catch 20 or more fishes to spawn");
                case ("08BF"): //Char
                    return ("Catch 20 or more fishes to spawn");
                case ("1061"): //Golden Trout
                    return ("Catch 100 or more fishes to spawn");
                case ("08C1"): //Stringfish
                    return ("Catch 100 or more fishes to spawn");
                case ("08C3"): //King Salmon
                    return ("Catch 20 or more fishes to spawn");
                case ("08C4"): //Mitten Crab
                    return ("Catch 20 or more fishes to spawn");
                case ("08C6"): //Nibble Fish
                    return ("Catch 20 or more fishes to spawn");
                case ("08C7"): //Angelfish
                    return ("Catch 20 or more fishes to spawn");
                case ("105F"): //Betta
                    return ("Catch 20 or more fishes to spawn");
                case ("08C9"): //Piranha
                    return ("Catch 20 or more fishes to spawn");
                case ("08CA"): //Arowana
                    return ("Catch 50 or more fishes to spawn");
                case ("08CB"): //Dorado
                    return ("Catch 100 or more fishes to spawn");
                case ("08CC"): //Gar
                    return ("Catch 50 or more fishes to spawn");
                case ("08CD"): //Arapaima
                    return ("Catch 50 or more fishes to spawn");
                case ("08CE"): //Saddled Bichir
                    return ("Catch 20 or more fishes to spawn");
                case ("105D"): //Sturgeon
                    return ("Catch 20 or more fishes to spawn");


                case ("08D4"): //Napoleonfish
                    return ("Catch 50 or more fishes to spawn");
                case ("08D6"): //Blowfish
                    return ("Catch 20 or more fishes to spawn");
                case ("08D9"): //Barred Knifejaw
                    return ("Catch 20 or more fishes to spawn");
                case ("08DF"): //Moray Eel
                    return ("Catch 20 or more fishes to spawn");
                case ("08E2"): //Tuna
                    return ("Catch 50 or more fishes to spawn");
                case ("08E3"): //Blue Marlin
                    return ("Catch 50 or more fishes to spawn");
                case ("08E4"): //Giant Trevally
                    return ("Catch 20 or more fishes to spawn");
                case ("106A"): //Mahi-mahi
                    return ("Catch 50 or more fishes to spawn");
                case ("08E6"): //Ocean Sunfish
                    return ("Catch 20 or more fishes to spawn");
                case ("08E5"): //Ray
                    return ("Catch 20 or more fishes to spawn");
                case ("08E9"): //Saw Shark
                    return ("Catch 50 or more fishes to spawn");
                case ("08E7"): //Hammerhead Shark
                    return ("Catch 20 or more fishes to spawn");
                case ("08E8"): //Great White Shark
                    return ("Catch 50 or more fishes to spawn");
                case ("08EA"): //Whale Shark
                    return ("Catch 50 or more fishes to spawn");
                case ("106B"): //Suckerfish
                    return ("Catch 20 or more fishes to spawn");
                case ("08E1"): //Football Fish
                    return ("Catch 20 or more fishes to spawn");
                case ("08EB"): //Oarfish
                    return ("Catch 50 or more fishes to spawn");
                case ("106C"): //Barreleye
                    return ("Catch 100 or more fishes to spawn");
                case ("08EC"): //Coelacanth
                    return ("Catch 100 or more fishes to spawn\nRainy days only");

            }
            return "";
        }

        #region Cell Click
        private void GridView_SelectionChanged(object sender, EventArgs e)
        {
            var senderGrid = (DataGridView)sender;
            senderGrid.ClearSelection();
        }

        private void GridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            var senderGrid = (DataGridView)sender;

            if (senderGrid.Columns[e.ColumnIndex] is DataGridViewButtonColumn && e.RowIndex >= 0)
            {
                if (senderGrid == InsectGridView)
                    CellContentClick(senderGrid, e.RowIndex, e.ColumnIndex, ref insectRate);
                else if (senderGrid == RiverFishGridView)
                    CellContentClick(senderGrid, e.RowIndex, e.ColumnIndex, ref riverFishRate);
                else if (senderGrid == SeaFishGridView)
                    CellContentClick(senderGrid, e.RowIndex, e.ColumnIndex, ref seaFishRate);
                else if (senderGrid == SeaCreatureGridView)
                    CellContentClick(senderGrid, e.RowIndex, e.ColumnIndex, ref seaCreatureRate);
            }
        }

        private void CellContentClick(DataGridView grid, int row, int col, ref int[] rate)
        {
            DataGridViewCellStyle btnStyle = new DataGridViewCellStyle
            {
                BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218))))),
                Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };

            DataGridViewCellStyle selectedbtnStyle = new DataGridViewCellStyle
            {
                BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255))))),
                Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };

            var Minbtn = (DataGridViewButtonCell)grid.Rows[row].Cells[6];
            var Defbtn = (DataGridViewButtonCell)grid.Rows[row].Cells[8];
            var Maxbtn = (DataGridViewButtonCell)grid.Rows[row].Cells[10];

            var cell = (DataGridViewProgressCell)grid.Rows[row].Cells[5];
            var index = (int)grid.Rows[row].Cells[1].Value;
            if (col == 6)
            {
                rate[index] = 0;
                cell.setValue = 0;

                if (grid == InsectGridView)
                    setSpawnRate(InsectAppearParam, Utilities.InsectDataSize, (int)grid.Rows[row].Cells[1].Value, 0, 0);
                else if (grid == RiverFishGridView)
                    setSpawnRate(FishRiverAppearParam, Utilities.FishDataSize, (int)grid.Rows[row].Cells[1].Value, 0, 1);
                else if (grid == SeaFishGridView)
                    setSpawnRate(FishSeaAppearParam, Utilities.FishDataSize, (int)grid.Rows[row].Cells[1].Value, 0, 2);
                else if (grid == SeaCreatureGridView)
                    setSpawnRate(CreatureSeaAppearParam, Utilities.SeaCreatureDataSize, (int)grid.Rows[row].Cells[1].Value, 0, 3);

                Minbtn.Style = selectedbtnStyle;
                Defbtn.Style = btnStyle;
                Maxbtn.Style = btnStyle;
            }
            else if (col == 8)
            {
                if (grid.Rows[row].Cells[4].Value != null)
                {
                    rate[index] = (int)grid.Rows[row].Cells[4].Value;
                    cell.setValue = (int)grid.Rows[row].Cells[4].Value;
                    cell.remark = getRemark(String.Format("{0:X4}", grid.Rows[row].Cells[3].Value.ToString()));

                    if (grid == InsectGridView)
                        setSpawnRate(InsectAppearParam, Utilities.InsectDataSize, (int)grid.Rows[row].Cells[1].Value, 1, 0);
                    else if (grid == RiverFishGridView)
                        setSpawnRate(FishRiverAppearParam, Utilities.FishDataSize, (int)grid.Rows[row].Cells[1].Value, 1, 1);
                    else if (grid == SeaFishGridView)
                        setSpawnRate(FishSeaAppearParam, Utilities.FishDataSize, (int)grid.Rows[row].Cells[1].Value, 1, 2);
                    else if (grid == SeaCreatureGridView)
                        setSpawnRate(CreatureSeaAppearParam, Utilities.SeaCreatureDataSize, (int)grid.Rows[row].Cells[1].Value, 1, 3);

                    Minbtn.Style = btnStyle;
                    Defbtn.Style = selectedbtnStyle;
                    Maxbtn.Style = btnStyle;
                }
            }
            else if (col == 10)
            {
                rate[index] = 255;
                cell.setValue = 255;

                if (grid == InsectGridView)
                    setSpawnRate(InsectAppearParam, Utilities.InsectDataSize, (int)grid.Rows[row].Cells[1].Value, 2, 0);
                else if (grid == RiverFishGridView)
                    setSpawnRate(FishRiverAppearParam, Utilities.FishDataSize, (int)grid.Rows[row].Cells[1].Value, 2, 1);
                else if (grid == SeaFishGridView)
                    setSpawnRate(FishSeaAppearParam, Utilities.FishDataSize, (int)grid.Rows[row].Cells[1].Value, 2, 2);
                else if (grid == SeaCreatureGridView)
                    setSpawnRate(CreatureSeaAppearParam, Utilities.SeaCreatureDataSize, (int)grid.Rows[row].Cells[1].Value, 2, 3);

                Minbtn.Style = btnStyle;
                Defbtn.Style = btnStyle;
                Maxbtn.Style = selectedbtnStyle;
            }
            grid.InvalidateCell(cell);
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void setSpawnRate(byte[] source, int size, int index, int mode, int type)
        {
            if (source == null)
            {
                MessageBox.Show("Please load the critter data first.", "Missing critter data!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                int localIndex = index * 2;
                byte[] b;
                if (source == InsectAppearParam)
                    b = new byte[12 * 6 * 2];
                else
                    b = new byte[78]; //[12 * 3 * 2];


                if (mode == 0) // min
                {
                    for (int i = 0; i < b.Length; i++)
                        b[i] = 0;
                }
                else if (mode == 1) // default
                {
                    for (int i = 0; i < b.Length; i++)
                        b[i] = source[size * localIndex + 2 + i];
                }
                else if (mode == 2) // max
                    for (int i = 0; i < b.Length; i += 2)
                    {
                        b[i] = 0xFF;
                        b[i + 1] = 0;
                    }
                //Debug.Print(Encoding.UTF8.GetString(Utilities.transform(b)));
                Utilities.SendSpawnRate(socket, usb, b, localIndex, type, ref counter);
                localIndex++;
                if (mode == 1)
                {
                    for (int i = 0; i < b.Length; i++)
                        b[i] = source[size * localIndex + 2 + i];
                }
                Utilities.SendSpawnRate(socket, usb, b, localIndex, type, ref counter);
            }
            catch (Exception e)
            {
                MyMessageBox.Show(e.Message.ToString(), "NOTE: This isn't particularly efficient. Too bad!");
            }
        }
        #endregion

        #region Search box
        private void CritterSearchBox_TextChanged(object sender, EventArgs e)
        {
            try
            {
                if (currentGridView.DataSource == null)
                    return;
                if (currentGridView == InsectGridView)
                    SearchBox_TextChanged(currentGridView, ref insectRate);
                else if (currentGridView == RiverFishGridView)
                    SearchBox_TextChanged(currentGridView, ref riverFishRate, 1);
                else if (currentGridView == SeaFishGridView)
                    SearchBox_TextChanged(currentGridView, ref seaFishRate, 1);
                else if (currentGridView == SeaCreatureGridView)
                    SearchBox_TextChanged(currentGridView, ref seaCreatureRate, 1);
            }
            catch
            {
                CritterSearchBox.Clear();
            }
        }

        private void SearchBox_TextChanged(DataGridView grid, ref int[] rate, int mode = 0)
        {
            (grid.DataSource as DataTable).DefaultView.RowFilter = string.Format("Name LIKE '%{0}%'", CritterSearchBox.Text);

            DataGridViewCellStyle btnStyle = new DataGridViewCellStyle
            {
                BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218))))),
                Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };

            DataGridViewCellStyle selectedbtnStyle = new DataGridViewCellStyle
            {
                BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255))))),
                Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };

            for (int i = 0; i < grid.Rows.Count; i++)
            {
                var cell = (DataGridViewProgressCell)grid.Rows[i].Cells[5];
                var Minbtn = (DataGridViewButtonCell)grid.Rows[i].Cells[6];
                var Defbtn = (DataGridViewButtonCell)grid.Rows[i].Cells[8];
                var Maxbtn = (DataGridViewButtonCell)grid.Rows[i].Cells[10];
                int spawnrate = rate[(int)grid.Rows[i].Cells[1].Value];

                cell.setValue = spawnrate;
                cell.remark = getRemark(String.Format("{0:X4}", grid.Rows[i].Cells[3].Value.ToString()));
                cell.mode = mode;
                if (spawnrate <= 0)
                {
                    Minbtn.Style = selectedbtnStyle;
                    Defbtn.Style = btnStyle;
                    Maxbtn.Style = btnStyle;
                }
                else if (spawnrate >= 255)
                {
                    Minbtn.Style = btnStyle;
                    Defbtn.Style = btnStyle;
                    Maxbtn.Style = selectedbtnStyle;
                }
            }
        }

        private void CritterSearchBox_Click(object sender, EventArgs e)
        {
            if (CritterSearchBox.Text == "Search...")
                CritterSearchBox.Clear();
        }
        #endregion

        #region GridView Add Image
        private void GridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var senderGrid = (DataGridView)sender;

            if (e.RowIndex >= 0 && e.RowIndex < senderGrid.Rows.Count)
            {
                int row = e.RowIndex;
                if (e.ColumnIndex == 0)
                {
                    CellFormatting(senderGrid, row, e);
                }
            }
        }

        private void CellFormatting(DataGridView grid, int row, DataGridViewCellFormattingEventArgs e)
        {
            if (grid.Rows[row].Cells.Count <= 3)
                return;
            if (grid.Rows[row].Cells[3].Value == null)
                return;
            string Id = grid.Rows[row].Cells[3].Value.ToString();
            //Debug.Print(Id);
            string imagePath = GetImagePathFromID(Id, itemSource);
            if (imagePath != "")
            {
                Image image = Image.FromFile(imagePath);
                e.Value = image;
            }
        }
        #endregion

        #region Disable & Reset Button
        private void DisableAllButton_Click(object sender, EventArgs e)
        {
            if (CritterSearchBox.Text != "Search...")
            {
                CritterSearchBox.Clear();
            }

            if (currentGridView == InsectGridView)
                disableAll(currentGridView, ref insectRate);
            else if (currentGridView == RiverFishGridView)
                disableAll(currentGridView, ref riverFishRate, 1);
            else if (currentGridView == SeaFishGridView)
                disableAll(currentGridView, ref seaFishRate, 1);
            else if (currentGridView == SeaCreatureGridView)
                disableAll(currentGridView, ref seaCreatureRate, 1);
        }

        private void disableAll(DataGridView grid, ref int[] rate, int mode = 0)
        {
            if (rate == null)
                return;
            string temp = null;
            if (CritterSearchBox.Text != "Search...")
            {
                temp = CritterSearchBox.Text;
                CritterSearchBox.Clear();
            }
            //CritterSearchBox.Clear();

            for (int i = 0; i < rate.Length; i++)
            {
                rate[i] = 0;
            }

            disableBtn();

            Thread disableThread = new Thread(delegate () { disableAll(grid, mode, temp); });
            disableThread.Start();
        }

        private void disableAll(DataGridView grid, int mode, string temp)
        {

            DataGridViewCellStyle btnStyle = new DataGridViewCellStyle
            {
                BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218))))),
                Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };

            DataGridViewCellStyle selectedbtnStyle = new DataGridViewCellStyle
            {
                BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255))))),
                Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };

            for (int i = 0; i < grid.Rows.Count; i++)
            {
                var cell = (DataGridViewProgressCell)grid.Rows[i].Cells[5];
                var Minbtn = (DataGridViewButtonCell)grid.Rows[i].Cells[6];
                var Defbtn = (DataGridViewButtonCell)grid.Rows[i].Cells[8];
                var Maxbtn = (DataGridViewButtonCell)grid.Rows[i].Cells[10];

                if (grid == InsectGridView)
                    setSpawnRate(InsectAppearParam, Utilities.InsectDataSize, (int)grid.Rows[i].Cells[1].Value, 0, 0);
                else if (grid == RiverFishGridView)
                    setSpawnRate(FishRiverAppearParam, Utilities.FishDataSize, (int)grid.Rows[i].Cells[1].Value, 0, 1);
                else if (grid == SeaFishGridView)
                    setSpawnRate(FishSeaAppearParam, Utilities.FishDataSize, (int)grid.Rows[i].Cells[1].Value, 0, 2);
                else if (grid == SeaCreatureGridView)
                    setSpawnRate(CreatureSeaAppearParam, Utilities.SeaCreatureDataSize, (int)grid.Rows[i].Cells[1].Value, 0, 3);

                cell.setValue = 0;
                cell.remark = getRemark(String.Format("{0:X4}", grid.Rows[i].Cells[3].Value.ToString()));
                cell.mode = mode;
                Minbtn.Style = selectedbtnStyle;
                Defbtn.Style = btnStyle;
                Maxbtn.Style = btnStyle;

                grid.InvalidateCell(cell);
            }

            Invoke((MethodInvoker)delegate
            {
                if (temp != null)
                    CritterSearchBox.Text = temp;
                enableBtn();
            });
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void ResetAllButton_Click(object sender, EventArgs e)
        {
            if (CritterSearchBox.Text != "Search...")
            {
                CritterSearchBox.Clear();
            }

            if (currentGridView == InsectGridView)
                resetAll(currentGridView, ref insectRate);
            else if (currentGridView == RiverFishGridView)
                resetAll(currentGridView, ref riverFishRate, 1);
            else if (currentGridView == SeaFishGridView)
                resetAll(currentGridView, ref seaFishRate, 1);
            else if (currentGridView == SeaCreatureGridView)
                resetAll(currentGridView, ref seaCreatureRate, 1);
        }

        private void resetAll(DataGridView grid, ref int[] rate, int mode = 0)
        {
            if (rate == null)
                return;
            string temp = null;
            if (CritterSearchBox.Text != "Search...")
            {
                temp = CritterSearchBox.Text;
                CritterSearchBox.Clear();
            }
            //CritterSearchBox.Clear();

            for (int i = 0; i < rate.Length; i++)
            {
                rate[i] = (int)grid.Rows[i].Cells[4].Value;
            }

            disableBtn();

            Thread resetThread = new Thread(delegate () { resetAll(grid, mode, temp); });
            resetThread.Start();
        }

        private void resetAll(DataGridView grid, int mode, string temp)
        {

            DataGridViewCellStyle btnStyle = new DataGridViewCellStyle
            {
                BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218))))),
                Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };

            DataGridViewCellStyle selectedbtnStyle = new DataGridViewCellStyle
            {
                BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255))))),
                Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };

            for (int i = 0; i < grid.Rows.Count; i++)
            {
                var cell = (DataGridViewProgressCell)grid.Rows[i].Cells[5];
                var Minbtn = (DataGridViewButtonCell)grid.Rows[i].Cells[6];
                var Defbtn = (DataGridViewButtonCell)grid.Rows[i].Cells[8];
                var Maxbtn = (DataGridViewButtonCell)grid.Rows[i].Cells[10];

                if (grid == InsectGridView)
                    setSpawnRate(InsectAppearParam, Utilities.InsectDataSize, (int)grid.Rows[i].Cells[1].Value, 1, 0);
                else if (grid == RiverFishGridView)
                    setSpawnRate(FishRiverAppearParam, Utilities.FishDataSize, (int)grid.Rows[i].Cells[1].Value, 1, 1);
                else if (grid == SeaFishGridView)
                    setSpawnRate(FishSeaAppearParam, Utilities.FishDataSize, (int)grid.Rows[i].Cells[1].Value, 1, 2);
                else if (grid == SeaCreatureGridView)
                    setSpawnRate(CreatureSeaAppearParam, Utilities.SeaCreatureDataSize, (int)grid.Rows[i].Cells[1].Value, 1, 3);

                cell.setValue = (int)grid.Rows[i].Cells[4].Value;
                cell.remark = getRemark(String.Format("{0:X4}", grid.Rows[i].Cells[3].Value.ToString()));
                cell.mode = mode;
                Minbtn.Style = btnStyle;
                Defbtn.Style = selectedbtnStyle;
                Maxbtn.Style = btnStyle;

                grid.InvalidateCell(cell);
            }

            Invoke((MethodInvoker)delegate
            {
                if (temp != null)
                    CritterSearchBox.Text = temp;
                enableBtn();
            });
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }
        #endregion

        #region Button Control
        private void disableBtn()
        {
            CritterNowLoadingPanel.Visible = true;

            DisableAllButton.Visible = false;
            ResetAllButton.Visible = false;
            ReadCritterDataButton.Visible = false;
            currentGridView.Enabled = false;
            CritterSearchBox.Enabled = false;

            InsectButton.Enabled = false;
            RiverFishButton.Enabled = false;
            SeaFishButton.Enabled = false;
            SeaCreatureButton.Enabled = false;
        }

        private void enableBtn()
        {
            CritterNowLoadingPanel.Visible = false;

            DisableAllButton.Visible = true;
            ResetAllButton.Visible = true;
            ReadCritterDataButton.Visible = true;
            currentGridView.Enabled = true;
            CritterSearchBox.Enabled = true;

            InsectButton.Enabled = true;
            RiverFishButton.Enabled = true;
            SeaFishButton.Enabled = true;
            SeaCreatureButton.Enabled = true;
        }
        #endregion



        #region Mode Button
        private void InsectButton_Click(object sender, EventArgs e)
        {
            if (CritterSearchBox.Text != "Search...")
            {
                CritterSearchBox.Clear();
            }

            currentGridView = InsectGridView;

            InsectButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            RiverFishButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            SeaFishButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            SeaCreatureButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));

            InsectGridView.Visible = true;
            RiverFishGridView.Visible = false;
            SeaFishGridView.Visible = false;
            SeaCreatureGridView.Visible = false;

            InsectGridView.ClearSelection();
        }

        private void RiverFishButton_Click(object sender, EventArgs e)
        {
            if (CritterSearchBox.Text != "Search...")
            {
                CritterSearchBox.Clear();
            }

            currentGridView = RiverFishGridView;

            InsectButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            RiverFishButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            SeaFishButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            SeaCreatureButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));


            InsectGridView.Visible = false;
            RiverFishGridView.Visible = true;
            SeaFishGridView.Visible = false;
            SeaCreatureGridView.Visible = false;

            RiverFishGridView.ClearSelection();
        }

        private void SeaFishButton_Click(object sender, EventArgs e)
        {

            if (CritterSearchBox.Text != "Search...")
            {
                CritterSearchBox.Clear();
            }

            currentGridView = SeaFishGridView;

            InsectButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            RiverFishButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            SeaFishButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));
            SeaCreatureButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));


            InsectGridView.Visible = false;
            RiverFishGridView.Visible = false;
            SeaFishGridView.Visible = true;
            SeaCreatureGridView.Visible = false;

            SeaFishGridView.ClearSelection();
        }

        private void SeaCreatureButton_Click(object sender, EventArgs e)
        {
            if (CritterSearchBox.Text != "Search...")
            {
                CritterSearchBox.Clear();
            }

            currentGridView = SeaCreatureGridView;

            InsectButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            RiverFishButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            SeaFishButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(114)))), ((int)(((byte)(137)))), ((int)(((byte)(218)))));
            SeaCreatureButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(255)))));


            InsectGridView.Visible = false;
            RiverFishGridView.Visible = false;
            SeaFishGridView.Visible = false;
            SeaCreatureGridView.Visible = true;

            SeaCreatureGridView.ClearSelection();
        }
        #endregion

        #region Read Data
        private void ReadCritterDataButton_Click(object sender, EventArgs e)
        {
            disableBtn();

            Thread readThread = new Thread(delegate () { readData(); });
            readThread.Start();

        }

        private void readData()
        {
            try
            {
                if (currentGridView == InsectGridView)
                {
                    InsectAppearParam = Utilities.GetCritterData(socket, usb, 0);
                    File.WriteAllBytes(insectAppearFileName, InsectAppearParam);

                    Invoke((MethodInvoker)delegate
                    {
                        InsectGridView.DataSource = null;
                        InsectGridView.Rows.Clear();
                        InsectGridView.Columns.Clear();
                        LoadGridView(InsectAppearParam, InsectGridView, ref insectRate, Utilities.InsectDataSize, Utilities.InsectNumRecords);
                    });
                }
                else if (currentGridView == RiverFishGridView)
                {
                    FishRiverAppearParam = Utilities.GetCritterData(socket, usb, 1);
                    File.WriteAllBytes(fishRiverAppearFileName, FishRiverAppearParam);

                    Invoke((MethodInvoker)delegate
                    {
                        RiverFishGridView.DataSource = null;
                        RiverFishGridView.Rows.Clear();
                        RiverFishGridView.Columns.Clear();
                        LoadGridView(FishRiverAppearParam, RiverFishGridView, ref riverFishRate, Utilities.FishDataSize, Utilities.FishRiverNumRecords, 1);
                    });
                }
                else if (currentGridView == SeaFishGridView)
                {
                    FishSeaAppearParam = Utilities.GetCritterData(socket, usb, 2);
                    File.WriteAllBytes(fishSeaAppearFileName, FishSeaAppearParam);

                    Invoke((MethodInvoker)delegate
                    {
                        SeaFishGridView.DataSource = null;
                        SeaFishGridView.Rows.Clear();
                        SeaFishGridView.Columns.Clear();
                        LoadGridView(FishSeaAppearParam, SeaFishGridView, ref seaFishRate, Utilities.FishDataSize, Utilities.FishSeaNumRecords, 1);
                    });
                }
                else if (currentGridView == SeaCreatureGridView)
                {
                    CreatureSeaAppearParam = Utilities.GetCritterData(socket, usb, 3);
                    File.WriteAllBytes(CreatureSeaAppearFileName, CreatureSeaAppearParam);

                    Invoke((MethodInvoker)delegate
                    {
                        SeaCreatureGridView.DataSource = null;
                        SeaCreatureGridView.Rows.Clear();
                        SeaCreatureGridView.Columns.Clear();
                        LoadGridView(CreatureSeaAppearParam, SeaCreatureGridView, ref seaCreatureRate, Utilities.SeaCreatureDataSize, Utilities.SeaCreatureNumRecords, 1);
                    });
                }
            }
            catch (Exception e)
            {
                MyMessageBox.Show(e.Message.ToString(), "This is a stupid fix, but I don't have time to da a cleaner implementation");
            }

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();

            Invoke((MethodInvoker)delegate
            {
                enableBtn();
            });
        }
        #endregion

        private void loadAllVillager()
        {
            lock (villagerLock)
            {
                if (usb == null)
                    showVillagerWait(25000, "Acquiring villager data...");
                else
                    showVillagerWait(15000, "Acquiring villager data...");

                if ((socket == null || socket.Connected == false) & usb == null)
                    return;

                VillagerLoading = true;
                selectedVillagerButton = null;

                HouseList = new int[10];

                for (int i = 0; i < 10; i++)
                {
                    byte b = Utilities.GetHouseOwner(socket, usb, i, ref counter);
                    if (b == 0xDD)
                    {
                        hideVillagerWait();
                        return;
                    }
                    HouseList[i] = Convert.ToInt32(b);
                }
                Debug.Print(string.Join(" ", HouseList));


                V = new Villager[10];
                villagerButton = new Button[10];

                for (int i = 0; i < 10; i++)
                {
                    byte[] b = Utilities.GetVillager(socket, usb, i, (int)(Utilities.VillagerMemoryTinySize), ref counter);
                    V[i] = new Villager(b, i)
                    {
                        HouseIndex = Utilities.FindHouseIndex(i, HouseList)
                    };

                    byte f = Utilities.GetVillagerHouseFlag(socket, usb, V[i].HouseIndex, 0x8, ref counter);
                    V[i].MoveInFlag = Convert.ToInt32(f);

                    byte[] move = Utilities.GetMoveout(socket, usb, i, (int)0x33, ref counter);
                    V[i].AbandonedHouseFlag = Convert.ToInt32(move[0]);
                    V[i].InvitedFlag = Convert.ToInt32(move[0x14]);
                    V[i].ForceMoveOutFlag = Convert.ToInt32(move[move.Length - 1]);
                    byte[] catchphrase = Utilities.GetCatchphrase(socket, usb, i, ref counter);
                    V[i].catchphrase = catchphrase;

                    villagerButton[i] = new Button();
                    villagerButton[i].TextAlign = System.Drawing.ContentAlignment.TopCenter;
                    villagerButton[i].ForeColor = System.Drawing.Color.White;
                    villagerButton[i].Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Bold);
                    int friendship = V[i].Friendship[0];
                    villagerButton[i].BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(friendship)))), ((int)(((byte)(friendship / 2)))), ((int)(((byte)(friendship)))));
                    villagerButton[i].FlatAppearance.BorderSize = 0;
                    villagerButton[i].FlatStyle = System.Windows.Forms.FlatStyle.Flat;
                    if (i < 5)
                        villagerButton[i].Location = new System.Drawing.Point((i * 128) + (i * 10) + 50, 54);
                    else
                        villagerButton[i].Location = new System.Drawing.Point(((i - 5) * 128) + ((i - 5) * 10) + 50, 192);
                    villagerButton[i].Name = "villagerBtn" + i.ToString();
                    villagerButton[i].Tag = i;
                    villagerButton[i].Size = new System.Drawing.Size(128, 128);
                    villagerButton[i].UseVisualStyleBackColor = false;
                    Image img;
                    if (V[i].GetRealName() == "ERROR")
                    {
                        string path = Utilities.GetVillagerImage(V[i].GetRealName());
                        if (!path.Equals(string.Empty))
                            img = Image.FromFile(path);
                        else
                            img = new Bitmap(Properties.Resources.Leaf, new Size(110, 110));
                    }
                    else
                    {
                        string path = Utilities.GetVillagerImage(V[i].GetInternalName());
                        if (!path.Equals(string.Empty))
                            img = Image.FromFile(path);
                        else
                            img = new Bitmap(Properties.Resources.Leaf, new Size(110, 110));
                    }

                    villagerButton[i].Text = V[i].GetRealName() + " : " + V[i].GetInternalName();

                    if (V[i].MoveInFlag == 0xC || V[i].MoveInFlag == 0xB)
                    {
                        if (V[i].IsReal())
                        {
                            if (V[i].IsInvited())
                                villagerButton[i].Text += "\n(Tom Nook Invited)";
                            else
                                villagerButton[i].Text += "\n(Moving In)";
                        }
                        else
                            villagerButton[i].Text += "\n(Just Move Out)";
                    }
                    else if (V[i].InvitedFlag == 0x2)
                        villagerButton[i].Text += "\n(Invited by Visitor)";
                    else if (V[i].AbandonedHouseFlag == 0x1 && V[i].ForceMoveOutFlag == 0x0)
                        villagerButton[i].Text += "\n(Floor Sweeping)";
                    else if (V[i].AbandonedHouseFlag == 0x2 && V[i].ForceMoveOutFlag == 0x1)
                        villagerButton[i].Text += "\n(Moving Out 1)";
                    else if (V[i].AbandonedHouseFlag == 0x2 && V[i].ForceMoveOutFlag == 0x0)
                        villagerButton[i].Text += "\n(Moving Out 2)";

                    villagerButton[i].Image = (Image)(new Bitmap(img, new Size(110, 110)));
                    villagerButton[i].ImageAlign = ContentAlignment.BottomCenter;
                    villagerButton[i].MouseDown += new System.Windows.Forms.MouseEventHandler(this.VillagerButton_MouseDown);
                }


                this.Invoke((MethodInvoker)delegate
                {
                    for (int i = 0; i < 10; i++)
                    {
                        this.VillagerLargePanel.Controls.Add(villagerButton[i]);
                        villagerButton[i].BringToFront();
                    }

                    VillagerIndex.Text = "";
                    VillagerName.Text = "";
                    VillagerIName.Text = "";
                    VillagerPersonality.Text = "";
                    VillagerFriendship.Text = "";
                    VillagerHouseIndex.Text = "";
                    VillagerCatchphrase.Text = "";

                    VillagerMoveInFlag.Text = "";
                    VillagerAbandonedHouseFlag.Text = "";
                    VillagerForceMoveoutFlag.Text = "";
                    VillagerHeader.Text = "";
                });

                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();


                VillagerLoading = false;

                hideVillagerWait();

            }
        }

        private void VillagerButton_MouseDown(object sender, MouseEventArgs e)
        {
            selectedVillagerButton = (Button)sender;

            RefreshVillagerUI(false);
        }

        private void VillagerProgressTimer_Tick(object sender, EventArgs e)
        {
            Invoke((MethodInvoker)delegate
            {
                if (counter <= VillagerNowLoadingProgressBar.Maximum)
                    VillagerNowLoadingProgressBar.Value = counter;
                else
                    VillagerNowLoadingProgressBar.Value = VillagerNowLoadingProgressBar.Maximum;
            });
        }

        private void showVillagerWait(int size, string msg = "")
        {
            this.Invoke((MethodInvoker)delegate
            {
                VillagerControlPanel.Enabled = false;
                VillagerNowLoadingLongMessage.SelectionAlignment = HorizontalAlignment.Center;
                VillagerNowLoadingLongMessage.Text = msg;
                counter = 0;
                if (usb == null)
                    VillagerNowLoadingProgressBar.Maximum = size / 500 + 5;
                else
                    VillagerNowLoadingProgressBar.Maximum = size / 300 + 5;
                Debug.Print("Max : " + VillagerNowLoadingProgressBar.Maximum.ToString());
                VillagerNowLoadingProgressBar.Value = counter;
                VillagerNowLoadingPanel.Visible = true;
                VillagerProgressTimer.Start();
            });
        }
        private void hideVillagerWait()
        {
            if (InvokeRequired)
            {
                MethodInvoker method = new MethodInvoker(hideVillagerWait);
                Invoke(method);
                return;
            }
            if (!VillagerLoading)
            {
                VillagerNowLoadingPanel.Visible = false;
                VillagerControlPanel.Enabled = true;
                if (VillagerNowLoadingPanel.Location.X != 780)
                    VillagerNowLoadingPanel.Location = new Point(780, 330);
                VillagerProgressTimer.Stop();
            }
        }

        public void RefreshVillagerUI(bool clear)
        {
            for (int j = 0; j < 10; j++)
            {
                int friendship = V[j].Friendship[0];
                if (villagerButton[j] != selectedVillagerButton)
                    villagerButton[j].BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(friendship)))), ((int)(((byte)(friendship / 2)))), ((int)(((byte)(friendship)))));

                villagerButton[j].Text = V[j].GetRealName() + " : " + V[j].GetInternalName();

                if (V[j].MoveInFlag == 0xC || V[j].MoveInFlag == 0xB)
                {
                    if (V[j].IsReal())
                    {
                        if (V[j].IsInvited())
                            villagerButton[j].Text += "\n(Tom Nook Invited)";
                        else
                            villagerButton[j].Text += "\n(Moving In)";
                    }
                    else
                        villagerButton[j].Text += "\n(Just Move Out)";
                }
                else if (V[j].InvitedFlag == 0x2)
                    villagerButton[j].Text += "\n(Invited by Visitor)";
                else if (V[j].AbandonedHouseFlag == 0x1 && V[j].ForceMoveOutFlag == 0x0)
                    villagerButton[j].Text += "\n(Floor Sweeping)";
                else if (V[j].AbandonedHouseFlag == 0x2 && V[j].ForceMoveOutFlag == 0x1)
                    villagerButton[j].Text += "\n(Moving Out 1)";
                else if (V[j].AbandonedHouseFlag == 0x2 && V[j].ForceMoveOutFlag == 0x0)
                    villagerButton[j].Text += "\n(Moving Out 2)";

                Image img;
                if (V[j].GetRealName() == "ERROR")
                {
                    string path = Utilities.GetVillagerImage(V[j].GetRealName());
                    if (!path.Equals(string.Empty))
                        img = Image.FromFile(path);
                    else
                        img = new Bitmap(Properties.Resources.Leaf, new Size(110, 110));
                }
                else
                {
                    string path = Utilities.GetVillagerImage(V[j].GetInternalName());
                    if (!path.Equals(string.Empty))
                        img = Image.FromFile(path);
                    else
                        img = new Bitmap(Properties.Resources.Leaf, new Size(110, 110));
                }
                villagerButton[j].Image = (Image)(new Bitmap(img, new Size(110, 110)));

            }
            if (!clear)
            {
                if (selectedVillagerButton != null)
                {
                    selectedVillagerButton.BackColor = System.Drawing.Color.LightSeaGreen;

                    int i = Int16.Parse(selectedVillagerButton.Tag.ToString());

                    if (VillagerHeaderIgnore.Checked)
                    {
                        VillagerControlPanel.Enabled = true;
                    }
                    else
                    {
                        if (VillagerLoading == false)
                            VillagerControlPanel.Enabled = true;
                        else
                            VillagerControlPanel.Enabled = false;

                        if (V[i].MoveInFlag == 0xC || V[i].MoveInFlag == 0xB)
                        {
                            if (V[i].IsReal())
                                VillagerControlPanel.Enabled = false;
                        }
                    }

                    VillagerIndex.Text = V[i].Index.ToString();
                    VillagerName.Text = V[i].GetRealName();
                    VillagerIName.Text = V[i].GetInternalName();
                    VillagerPersonality.Text = V[i].GetPersonality();
                    VillagerFriendship.Text = V[i].Friendship[0].ToString();
                    VillagerHouseIndex.Text = V[i].HouseIndex.ToString();
                    if (V[i].HouseIndex < 0 && V[i].IsReal())
                    {
                        VillagerHouseIndex.ForeColor = Color.Tomato;
                        VillagerHouseIndexLabel.ForeColor = Color.Tomato;
                    }
                    else
                    {
                        VillagerHouseIndex.ForeColor = Color.White;
                        VillagerHouseIndexLabel.ForeColor = Color.White;
                    }
                    VillagerCatchphrase.Text = Encoding.Unicode.GetString(V[i].catchphrase, 0, 44);

                    VillagerMoveInFlag.Text = "0x" + V[i].MoveInFlag.ToString("X");
                    VillagerAbandonedHouseFlag.Text = "0x" + V[i].AbandonedHouseFlag.ToString("X");
                    VillagerForceMoveoutFlag.Text = "0x" + V[i].ForceMoveOutFlag.ToString("X");
                    VillagerHeader.Text = Utilities.ByteToHexString(V[i].GetHeader());
                }
            }

            //if (sound)
            //System.Media.SystemSounds.Asterisk.Play();
        }

        private void CleanVillagerPage()
        {
            if (villagerButton != null)
            {
                for (int i = 0; i < 10; i++)
                    this.VillagerLargePanel.Controls.Remove(villagerButton[i]);
            }

            V = null;
            villagerButton = null;
            VillagerFirstLoad = true;
            VillagerNowLoadingPanel.Location = new Point(170, 150);
        }

        private void VillagerRefreshButton_Click(object sender, EventArgs e)
        {
            if (villagerButton != null)
            {
                for (int i = 0; i < 10; i++)
                    this.VillagerLargePanel.Controls.Remove(villagerButton[i]);
            }

            Thread LoadAllVillagerThread = new Thread(delegate () { loadAllVillager(); });
            LoadAllVillagerThread.Start();
        }

        private void FriendshipButton_Click(object sender, EventArgs e)
        {
            if (VillagerIndex.Text == "")
                return;
            int i = Int16.Parse(VillagerIndex.Text);

            Image img;
            string path;
            if (V[i].GetRealName() == "ERROR")
            {
                path = Utilities.GetVillagerImage(V[i].GetRealName());
            }
            else
            {
                path = Utilities.GetVillagerImage(V[i].GetInternalName());
            }

            if (!path.Equals(string.Empty))
                img = Image.FromFile(path);
            else
                img = new Bitmap(Properties.Resources.Leaf, new Size(128, 128));

            Friendship friendship = new Friendship(i, socket, usb, img, V[i], sound);
            friendship.ShowDialog();

            RefreshVillagerUI(false);
        }

        private void VillagerCatchphraseSetButton_Click(object sender, EventArgs e)
        {
            if (VillagerIndex.Text == "")
                return;
            int i = Int16.Parse(VillagerIndex.Text);
            byte[] phrase = new byte[44];
            byte[] temp = Encoding.Unicode.GetBytes(VillagerCatchphrase.Text);

            for (int j = 0; j < temp.Length; j++)
            {
                phrase[j] = temp[j];
            }

            Utilities.SetCatchphrase(socket, usb, i, phrase);

            V[i].catchphrase = phrase;
            RefreshVillagerUI(false);

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void VillagerCatchphraseClearButton_Click(object sender, EventArgs e)
        {
            if (VillagerIndex.Text == "")
                return;
            int i = Int16.Parse(VillagerIndex.Text);
            byte[] phrase = new byte[44];

            Utilities.SetCatchphrase(socket, usb, i, phrase);

            V[i].catchphrase = phrase;
            RefreshVillagerUI(false);

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void ReplaceVillagerButton_Click(object sender, EventArgs e)
        {
            if (VillagerIndex.Text == "")
                return;
            int i = Int16.Parse(VillagerIndex.Text);

            int j = V[i].HouseIndex;

            if (j > 9)
                return;

            int EmptyHouseNumber;

            if (j < 0)
            {
                EmptyHouseNumber = findEmptyHouse();
                if (EmptyHouseNumber >= 0)
                    j = EmptyHouseNumber;
                else
                    return;
            }

            if (VillagerReplaceSelector.SelectedIndex < 0)
                return;
            string[] lines = VillagerReplaceSelector.SelectedItem.ToString().Split(new string[] { " " }, StringSplitOptions.None);
            //byte[] IName = Encoding.Default.GetBytes(lines[lines.Length - 1]);
            string IName = lines[lines.Length - 1];
            string RealName = lines[0];
            string IVpath = Utilities.villagerPath + IName + ".nhv2";
            string RVpath = Utilities.villagerPath + RealName + ".nhv2";
            byte[] villagerData;
            byte[] houseData;

            if (checkDuplicate(IName))
            {
                DialogResult dialogResult = MyMessageBox.Show(RealName + " is currently living on your island!" +
                    "                                                   \nAre you sure you want to continue the replacement?" +
                    "                                                   \nNote that the game will attempt to remove any duplicated villager!", "Villager already exists!", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                if (dialogResult == DialogResult.No)
                {
                    return;
                }
            }

            if (File.Exists(IVpath))
            {
                Debug.Print("FOUND: " + IVpath);
                villagerData = File.ReadAllBytes(IVpath);
            }
            else if (File.Exists(RVpath))
            {
                Debug.Print("FOUND: " + RVpath);
                villagerData = File.ReadAllBytes(RVpath);
            }
            else
            {
                MessageBox.Show("Villager files \"" + IName + ".nhv2\" " + "/ \"" + RealName + ".nhv2\" " + "not found!", "Villager file not found");
                return;
            }

            string IHpath = Utilities.villagerPath + IName + ".nhvh2";
            string RHpath = Utilities.villagerPath + RealName + ".nhvh2";
            if (File.Exists(IHpath))
            {
                Debug.Print("FOUND: " + IHpath);
                houseData = File.ReadAllBytes(IHpath);
            }
            else if (File.Exists(RHpath))
            {
                Debug.Print("FOUND: " + RHpath);
                houseData = File.ReadAllBytes(RHpath);
            }
            else
            {
                MessageBox.Show("Villager house files \"" + IName + ".nhvh2\" " + "/ \"" + RealName + ".nhvh2\" " + "not found!", "House file not found");
                return;
            }

            string msg = "Replacing Villager...";

            Thread LoadBothThread = new Thread(delegate () { LoadBoth(i, j, villagerData, houseData, msg); });
            LoadBothThread.Start();
        }

        private void LoadBoth(int i, int j, byte[] villager, byte[] house, string msg)
        {
            if (villager.Length != Utilities.VillagerSize)
            {
                MessageBox.Show("Villager file size incorrect!", "Villager file invalid");
                return;
            }
            if (house.Length != Utilities.VillagerHouseSize)
            {
                MessageBox.Show("House file size incorrect!", "House file invalid");
                return;
            }

            showVillagerWait((int)Utilities.VillagerSize * 2 + (int)Utilities.VillagerHouseSize * 2, msg);

            VillagerLoading = true;

            byte[] modifiedVillager = villager;
            if (!VillagerHeaderIgnore.Checked)
            {
                if (header[0] != 0x0 && header[1] != 0x0 && header[2] != 0x0)
                {
                    Buffer.BlockCopy(header, 0x0, modifiedVillager, 0x4, 52);
                }
            }

            V[i].LoadData(modifiedVillager);

            V[i].AbandonedHouseFlag = Convert.ToInt32(villager[Utilities.VillagerMoveoutOffset]);
            V[i].ForceMoveOutFlag = Convert.ToInt32(villager[Utilities.VillagerForceMoveoutOffset]);

            byte[] phrase = new byte[44];
            Buffer.BlockCopy(villager, (int)Utilities.VillagerCatchphraseOffset, phrase, 0x0, 44);
            V[i].catchphrase = phrase;


            byte[] modifiedHouse = house;

            byte h = (Byte)i;
            modifiedHouse[Utilities.VillagerHouseOwnerOffset] = h;
            V[i].HouseIndex = j;
            HouseList[j] = i;

            Utilities.LoadVillager(socket, usb, i, modifiedVillager, ref counter);
            Utilities.LoadHouse(socket, usb, j, modifiedHouse, ref counter);

            this.Invoke((MethodInvoker)delegate
            {
                RefreshVillagerUI(false);
            });

            VillagerLoading = false;

            hideVillagerWait();
        }

        private void ForcedMoveoutButton_Click(object sender, EventArgs e)
        {
            if (VillagerIndex.Text == "")
                return;
            int i = Int16.Parse(VillagerIndex.Text);

            Utilities.SetMoveout(socket, usb, i);

            V[i].AbandonedHouseFlag = 2;
            V[i].ForceMoveOutFlag = 1;
            V[i].InvitedFlag = 0;
            RefreshVillagerUI(false);

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void IrregularMoveoutButton_Click(object sender, EventArgs e)
        {
            if (VillagerIndex.Text == "")
                return;
            int i = Int16.Parse(VillagerIndex.Text);

            Utilities.SetMoveout(socket, usb, i, "2", "0");

            V[i].AbandonedHouseFlag = 2;
            V[i].ForceMoveOutFlag = 0;
            V[i].InvitedFlag = 0;
            RefreshVillagerUI(false);

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void CancelMoveoutButton_Click(object sender, EventArgs e)
        {
            if (VillagerIndex.Text == "")
                return;
            int i = Int16.Parse(VillagerIndex.Text);

            Utilities.SetMoveout(socket, usb, i, "0", "0");

            V[i].AbandonedHouseFlag = 0;
            V[i].ForceMoveOutFlag = 0;
            V[i].InvitedFlag = 0;
            RefreshVillagerUI(false);

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void ForcedMoveoutAllButton_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 10; i++)
            {
                Utilities.SetMoveout(socket, usb, i);

                V[i].AbandonedHouseFlag = 2;
                V[i].ForceMoveOutFlag = 1;
                V[i].InvitedFlag = 0;
            }
            RefreshVillagerUI(false);

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void IrregularMoveoutAllButton_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 10; i++)
            {
                Utilities.SetMoveout(socket, usb, i, "2", "0");

                V[i].AbandonedHouseFlag = 2;
                V[i].ForceMoveOutFlag = 0;
                V[i].InvitedFlag = 0;
            }
            RefreshVillagerUI(false);

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void CancelMoveoutAllButton_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 10; i++)
            {
                Utilities.SetMoveout(socket, usb, i, "0", "0");

                V[i].AbandonedHouseFlag = 0;
                V[i].ForceMoveOutFlag = 0;
                V[i].InvitedFlag = 0;
            }
            RefreshVillagerUI(false);

            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void SaveVillagerButton_Click(object sender, EventArgs e)
        {
            if (VillagerIndex.Text == "")
                return;
            int i = Int16.Parse(VillagerIndex.Text);

            SaveFileDialog file = new SaveFileDialog()
            {
                Filter = "New Horizons Villager (*.nhv2)|*.nhv2",
                FileName = V[i].GetInternalName() + ".nhv2",
            };

            Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath.Replace(".exe", ".dll"));

            string savepath;

            if (config.AppSettings.Settings["LastSave"].Value.Equals(string.Empty))
                savepath = Directory.GetCurrentDirectory() + @"\save";
            else
                savepath = config.AppSettings.Settings["LastSave"].Value;

            if (Directory.Exists(savepath))
            {
                file.InitialDirectory = savepath;
            }
            else
            {
                file.InitialDirectory = @"C:\";
            }

            if (file.ShowDialog() != DialogResult.OK)
                return;

            string[] temp = file.FileName.Split('\\');
            string path = "";
            for (int j = 0; j < temp.Length - 1; j++)
                path = path + temp[j] + "\\";

            config.AppSettings.Settings["LastSave"].Value = path;
            config.Save(ConfigurationSaveMode.Minimal);

            Thread dumpThread = new Thread(delegate () { SaveVillager(i, file); });
            dumpThread.Start();
        }

        private void SaveVillager(int i, SaveFileDialog file)
        {
            showVillagerWait((int)Utilities.VillagerSize, "Dumping " + V[i].GetRealName() + " ...");

            VillagerLoading = true;

            byte[] VillagerData = Utilities.GetVillager(socket, usb, i, (int)Utilities.VillagerSize, ref counter);
            File.WriteAllBytes(file.FileName, VillagerData);

            byte[] CheckData = File.ReadAllBytes(file.FileName);
            byte[] CheckHeader = new byte[52];
            if (header[0] != 0x0 && header[1] != 0x0 && header[2] != 0x0)
            {
                Buffer.BlockCopy(CheckData, 0x4, CheckHeader, 0x0, 52);
            }

            if (!CheckHeader.SequenceEqual(header))
            {
                Debug.Print(Utilities.ByteToHexString(CheckHeader));
                Debug.Print(Utilities.ByteToHexString(header));
                MessageBox.Show("Wait something is wrong here!? \n\n Header Mismatch!", "Warning");
            }
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();

            VillagerLoading = false;

            hideVillagerWait();
        }

        private void SaveHouseButton_Click(object sender, EventArgs e)
        {
            if (VillagerIndex.Text == "")
                return;
            int i = Int16.Parse(VillagerIndex.Text);

            int j = V[i].HouseIndex;

            if (j < 0 || j > 9)
                return;

            SaveFileDialog file = new SaveFileDialog()
            {
                Filter = "New Horizons Villager House (*.nhvh2)|*.nhvh2",
                FileName = V[i].GetInternalName() + ".nhvh2",
            };

            Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath.Replace(".exe", ".dll"));

            string savepath;

            if (config.AppSettings.Settings["LastSave"].Value.Equals(string.Empty))
                savepath = Directory.GetCurrentDirectory() + @"\save";
            else
                savepath = config.AppSettings.Settings["LastSave"].Value;

            if (Directory.Exists(savepath))
            {
                file.InitialDirectory = savepath;
            }
            else
            {
                file.InitialDirectory = @"C:\";
            }

            if (file.ShowDialog() != DialogResult.OK)
                return;

            string[] temp = file.FileName.Split('\\');
            string path = "";
            for (int k = 0; k < temp.Length - 1; k++)
                path = path + temp[k] + "\\";

            config.AppSettings.Settings["LastSave"].Value = path;
            config.Save(ConfigurationSaveMode.Minimal);

            Thread dumpThread = new Thread(delegate () { SaveHouse(i, j, file); });
            dumpThread.Start();
        }

        private void SaveHouse(int i, int j, SaveFileDialog file)
        {
            showVillagerWait((int)Utilities.VillagerHouseSize, "Dumping " + V[i].GetRealName() + "'s House ...");

            byte[] house = Utilities.GetHouse(socket, usb, j, ref counter);
            File.WriteAllBytes(file.FileName, house);
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();

            hideVillagerWait();
        }

        private void LoadVillagerButton_Click(object sender, EventArgs e)
        {
            if (VillagerIndex.Text == "")
                return;
            int i = Int16.Parse(VillagerIndex.Text);

            OpenFileDialog file = new OpenFileDialog()
            {
                Filter = "New Horizons Villager (*.nhv2)|*.nhv2|New Horizons Villager (*.nhv)|*.nhv",
                //FileName = V[i].GetInternalName() + ".nhv",
            };

            Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath.Replace(".exe", ".dll"));

            string savepath;

            if (config.AppSettings.Settings["LastLoad"].Value.Equals(string.Empty))
                savepath = Directory.GetCurrentDirectory() + @"\save";
            else
                savepath = config.AppSettings.Settings["LastLoad"].Value;

            if (Directory.Exists(savepath))
            {
                file.InitialDirectory = savepath;
            }
            else
            {
                file.InitialDirectory = @"C:\";
            }

            if (file.ShowDialog() != DialogResult.OK)
                return;

            string[] temp = file.FileName.Split('\\');
            string path = "";
            for (int j = 0; j < temp.Length - 1; j++)
                path = path + temp[j] + "\\";

            config.AppSettings.Settings["LastLoad"].Value = path;
            config.Save(ConfigurationSaveMode.Minimal);

            byte[] data = File.ReadAllBytes(file.FileName);

            if (data.Length == Utilities.VillagerOldSize)
            {
                data = ConvertToNew(data);
            }

            if (data.Length != Utilities.VillagerSize)
            {
                MessageBox.Show("Villager file size incorrect!", "Villager file invalid");
                return;
            }

            string msg = "Loading villager...";

            Thread LoadVillagerThread = new Thread(delegate () { LoadVillager(i, data, msg); });
            LoadVillagerThread.Start();
        }

        private void LoadVillager(int i, byte[] villager, string msg)
        {
            showVillagerWait((int)Utilities.VillagerSize * 2, msg);

            VillagerLoading = true;

            byte[] modifiedVillager = villager;
            if (!VillagerHeaderIgnore.Checked)
            {
                if (header[0] != 0x0 && header[1] != 0x0 && header[2] != 0x0)
                {
                    Buffer.BlockCopy(header, 0x0, modifiedVillager, 0x4, 52);
                }
            }

            V[i].LoadData(modifiedVillager);

            //byte[] move = Utilities.GetMoveout(s, bot, i, (int)0x33, ref counter);
            V[i].AbandonedHouseFlag = Convert.ToInt32(villager[Utilities.VillagerMoveoutOffset]);
            V[i].ForceMoveOutFlag = Convert.ToInt32(villager[Utilities.VillagerForceMoveoutOffset]);

            byte[] phrase = new byte[44];
            //buffer.BlockCopy(Bank01to20, slotOffset, slotBytes, 0x0, 0x4);
            Buffer.BlockCopy(villager, (int)Utilities.VillagerCatchphraseOffset, phrase, 0x0, 44);
            V[i].catchphrase = phrase;

            Utilities.LoadVillager(socket, usb, i, modifiedVillager, ref counter);

            this.Invoke((MethodInvoker)delegate
            {
                RefreshVillagerUI(false);
            });

            VillagerLoading = false;

            hideVillagerWait();
        }

        private void LoadHouseButton_Click(object sender, EventArgs e)
        {
            if (VillagerIndex.Text == "")
                return;
            int i = Int16.Parse(VillagerIndex.Text);

            int j = V[i].HouseIndex;

            if (j < 0 || j > 9)
                return;

            OpenFileDialog file = new OpenFileDialog()
            {
                Filter = "New Horizons Villager House (*.nhvh2)|*.nhvh2",
                //FileName = V[i].GetInternalName() + ".nhvh2",
            };

            Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath.Replace(".exe", ".dll"));

            string savepath;

            if (config.AppSettings.Settings["LastLoad"].Value.Equals(string.Empty))
                savepath = Directory.GetCurrentDirectory() + @"\save";
            else
                savepath = config.AppSettings.Settings["LastLoad"].Value;

            if (Directory.Exists(savepath))
            {
                file.InitialDirectory = savepath;
            }
            else
            {
                file.InitialDirectory = @"C:\";
            }

            if (file.ShowDialog() != DialogResult.OK)
                return;

            string[] temp = file.FileName.Split('\\');
            string path = "";
            for (int k = 0; k < temp.Length - 1; k++)
                path = path + temp[k] + "\\";

            config.AppSettings.Settings["LastLoad"].Value = path;
            config.Save(ConfigurationSaveMode.Minimal);

            byte[] data = File.ReadAllBytes(file.FileName);

            if (data.Length == Utilities.VillagerHouseOldSize)
            {
                data = ConvertToNewHouse(data);
            }

            if (data.Length != Utilities.VillagerHouseSize)
            {
                MessageBox.Show("House file size incorrect!", "House file invalid");
                return;
            }

            string msg = "Loading house...";

            Thread LoadHouseThread = new Thread(delegate () { LoadHouse(i, j, data, msg); });
            LoadHouseThread.Start();

        }

        private void LoadHouse(int i, int j, byte[] house, string msg)
        {
            showVillagerWait((int)Utilities.VillagerHouseSize * 2, msg);

            byte[] modifiedHouse = house;

            byte h = (Byte)i;
            modifiedHouse[Utilities.VillagerHouseOwnerOffset] = h;
            V[i].HouseIndex = j;
            HouseList[j] = i;

            Utilities.LoadHouse(socket, usb, j, modifiedHouse, ref counter);

            this.Invoke((MethodInvoker)delegate
            {
                RefreshVillagerUI(false);
            });

            hideVillagerWait();
        }

        private void ReadMysVillagerButton_Click(object sender, EventArgs e)
        {
            byte[] IName = Utilities.GetMysVillagerName(socket, usb);
            string StrName = Encoding.ASCII.GetString(Utilities.ByteTrim(IName));
            string RealName = Utilities.GetVillagerRealName(StrName);

            Image img;
            if (RealName == "ERROR")
            {
                string path = Utilities.GetVillagerImage(RealName);
                if (!path.Equals(string.Empty))
                    img = Image.FromFile(path);
                else
                    img = new Bitmap(Properties.Resources.Leaf, new Size(110, 110));
                MysVillagerDisplay.Text = "";
            }
            else
            {
                string path = Utilities.GetVillagerImage(StrName);
                if (!path.Equals(string.Empty))
                    img = Image.FromFile(path);
                else
                    img = new Bitmap(Properties.Resources.Leaf, new Size(110, 110));
                MysVillagerDisplay.Text = RealName + " : " + StrName;
            }
            MysVillagerDisplay.Image = (Image)(new Bitmap(img, new Size(110, 110)));
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private void ReplaceMysVillagerButton_Click(object sender, EventArgs e)
        {
            if (MysVillagerReplaceSelector.SelectedIndex < 0)
                return;
            string[] lines = MysVillagerReplaceSelector.SelectedItem.ToString().Split(new string[] { " " }, StringSplitOptions.None);
            byte[] IName = Encoding.Default.GetBytes(lines[lines.Length - 1]);
            byte[] species = new byte[1];
            species[0] = Utilities.CheckSpecies[(lines[lines.Length - 1]).Substring(0, 3)];

            Utilities.SetMysVillager(socket, usb, IName, species, ref counter);
            Image img;
            string path = Utilities.GetVillagerImage(lines[lines.Length - 1]);
            if (!path.Equals(String.Empty))
                img = Image.FromFile(path);
            else
                img = new Bitmap(Properties.Resources.Leaf, new Size(110, 110));
            MysVillagerDisplay.Text = lines[0] + " : " + lines[lines.Length - 1];
            MysVillagerDisplay.Image = (Image)(new Bitmap(img, new Size(110, 110)));
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private int findEmptyHouse()
        {
            for (int i = 0; i < 10; i++)
            {
                if (HouseList[i] == 255)
                    return i;
            }
            return -1;
        }

        private bool checkDuplicate(string iName)
        {
            for (int i = 0; i < 10; i++)
            {
                if (V[i].GetInternalName() == iName)
                    return true;
            }
            return false;
        }

        private static byte[] ConvertToNew(byte[] oldVillager)
        {
            byte[] newVillager = new byte[Utilities.VillagerSize];

            Array.Copy(oldVillager, 0, newVillager, 0, 0x2f84);

            for (int i = 0; i < 160; i++)
            {
                var src = 0x2f84 + (0x14C * i);
                var dest = 0x2f84 + (0x158 * i);

                Array.Copy(oldVillager, src, newVillager, dest, 0x14C);
            }

            Array.Copy(oldVillager, 0xff04, newVillager, 0x10684, oldVillager.Length - 0xff04);

            return newVillager;
        }

        public static byte[] ConvertToNewHouse(byte[] oldHouse)
        {
            byte[] newHouse = new byte[Utilities.VillagerHouseSize];

            byte[] NoItem = { 0xFE, 0xFF };

            byte[] NewHouseFooter =
            {
                0x4B, 0x13, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x4B, 0x13, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x9B, 0x13, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF8, 0x00, 0xF8, 0x00, 0xF8, 0x00, 0x40,
                0x10, 0x00, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x3F,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xFE, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };

            Array.Copy(oldHouse, 0, newHouse, 0, Utilities.VillagerHouseOldSize);

            for (int i = 0; i < 236; i++)
            {
                Array.Copy(NoItem, 0x0, newHouse, 0x1D8 + (i * 0xC), NoItem.Length);
            }

            Array.Copy(NewHouseFooter, 0x0, newHouse, 0x1270, NewHouseFooter.Length);

            return newHouse;
        }

        private void ReplaceVilllagerSearchBox_Click(object sender, EventArgs e)
        {
            ReplaceVilllagerSearchBox.Text = "";
            ReplaceVilllagerSearchBox.ForeColor = Color.White;
        }

        private void ReplaceMysVilllagerSearchBox_Click(object sender, EventArgs e)
        {
            ReplaceMysVilllagerSearchBox.Text = "";
            ReplaceMysVilllagerSearchBox.ForeColor = Color.White;
        }

        private void ReplaceVilllagerSearchBox_Leave(object sender, EventArgs e)
        {
            if (ReplaceVilllagerSearchBox.Text == "")
            {
                ReplaceVilllagerSearchBox.Text = "Search...";
                ReplaceVilllagerSearchBox.ForeColor = Color.FromArgb(255, 114, 118, 125);
            }
        }

        private void ReplaceMysVilllagerSearchBox_Leave(object sender, EventArgs e)
        {
            if (ReplaceMysVilllagerSearchBox.Text == "")
            {
                ReplaceMysVilllagerSearchBox.Text = "Search...";
                ReplaceMysVilllagerSearchBox.ForeColor = Color.FromArgb(255, 114, 118, 125);
            }
        }

        private void ReplaceVilllagerSearchBox_TextChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < VillagerReplaceSelector.Items.Count; i++)
            {
                string[] lines = VillagerReplaceSelector.Items[i].ToString().Split(new string[] { " " }, StringSplitOptions.None);
                string RealName = lines[0];
                if (ReplaceVilllagerSearchBox.Text.Split(new string[] { " " }, StringSplitOptions.None)[0].ToLower() == RealName.ToLower())
                {
                    VillagerReplaceSelector.SelectedIndex = i;
                    break;
                }
            }
        }

        private void ReplaceMysVilllagerSearchBox_TextChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < MysVillagerReplaceSelector.Items.Count; i++)
            {
                string[] lines = MysVillagerReplaceSelector.Items[i].ToString().Split(new string[] { " " }, StringSplitOptions.None);
                //byte[] IName = Encoding.Default.GetBytes(lines[lines.Length - 1]);
                //string IName = lines[lines.Length - 1];
                string RealName = lines[0];
                if (ReplaceMysVilllagerSearchBox.Text.Split(new string[] { " " }, StringSplitOptions.None)[0].ToLower() == RealName.ToLower())
                {
                    MysVillagerReplaceSelector.SelectedIndex = i;
                    break;
                }
            }
        }

        private void VillagerHeaderIgnore_CheckedChanged(object sender, EventArgs e)
        {
            RefreshVillagerUI(false);
        }

        private void AmountOrCountTextbox_DoubleClick(object sender, EventArgs e)
        {
            if (currentPanel == RecipeModePanel || currentPanel == FlowerModePanel)
                return;

            string id = Utilities.precedingZeros(IDTextbox.Text, 4);

            UInt16 IntId = Convert.ToUInt16("0x" + IDTextbox.Text, 16);

            if (Utilities.itemkind.ContainsKey(id))
            {
                int value = Utilities.CountByKind[Utilities.itemkind[id]];

                if (ItemAttr.hasFenceWithVariation(IntId))  // Fence Variation
                {
                    string hexValue = "00000000";
                    if (HexModeButton.Tag.ToString() == "Normal")
                    {
                        int decValue = Convert.ToInt32(AmountOrCountTextbox.Text) - 1;
                        if (decValue >= 0)
                            hexValue = Utilities.precedingZeros(decValue.ToString("X"), 8);
                    }
                    else
                        hexValue = Utilities.precedingZeros(AmountOrCountTextbox.Text, 8);


                    string front = Utilities.precedingZeros(hexValue, 8).Substring(0, 4);
                    string back = Utilities.precedingZeros(hexValue, 8).Substring(4, 4);

                    if (HexModeButton.Tag.ToString() == "Normal")
                    {
                        int decValue = value - 1;
                        if (decValue >= 0)
                            AmountOrCountTextbox.Text = (int.Parse(front + Utilities.precedingZeros(decValue.ToString("X"), 4), System.Globalization.NumberStyles.HexNumber) + 1).ToString();
                        else
                            AmountOrCountTextbox.Text = (int.Parse(front + Utilities.precedingZeros("0", 4), System.Globalization.NumberStyles.HexNumber) + 1).ToString();
                    }
                    else
                    {
                        int decValue = value - 1;
                        if (decValue >= 0)
                            AmountOrCountTextbox.Text = front + Utilities.precedingZeros(decValue.ToString("X"), 4);
                        else
                            AmountOrCountTextbox.Text = front + Utilities.precedingZeros("0", 4);
                    }
                }
                else
                {
                    if (HexModeButton.Tag.ToString() == "Normal")
                    {
                        AmountOrCountTextbox.Text = value.ToString();
                    }
                    else
                    {
                        int decValue = value - 1;
                        if (decValue >= 0)
                            AmountOrCountTextbox.Text = Utilities.precedingZeros(decValue.ToString("X"), 8);
                        else
                            AmountOrCountTextbox.Text = Utilities.precedingZeros("0", 8);
                    }
                }
            }
            else
            {
                if (HexModeButton.Tag.ToString() == "Normal")
                    AmountOrCountTextbox.Text = "1";
                else
                    AmountOrCountTextbox.Text = Utilities.precedingZeros("0", 8);
            }

            AmountOrCountTextbox_KeyUp(sender, null);
        }

        private void AmountOrCountTextbox_KeyUp(object sender, KeyEventArgs e)
        {
            if (IDTextbox.Text == "" | AmountOrCountTextbox.Text == "")
                return;

            string hexValue = "00000000";
            if (HexModeButton.Tag.ToString() == "Normal")
            {
                int decValue = Convert.ToInt32(AmountOrCountTextbox.Text) - 1;
                if (decValue >= 0)
                    hexValue = Utilities.precedingZeros(decValue.ToString("X"), 8);
            }
            else
                hexValue = Utilities.precedingZeros(AmountOrCountTextbox.Text, 8);

            if (IDTextbox.Text == "")
                return;

            UInt16 IntId = Convert.ToUInt16("0x" + IDTextbox.Text, 16);

            string front = Utilities.precedingZeros(hexValue, 8).Substring(0, 4);
            string back = Utilities.precedingZeros(hexValue, 8).Substring(4, 4);

            if (IDTextbox.Text == "16A2") //recipe
            {
                SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(hexValue), recipeSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(hexValue), recipeSource), true, "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
            }
            else if (IDTextbox.Text == "315A" || IDTextbox.Text == "1618" || IDTextbox.Text == "342F") // Wall-Mounted
            {
                SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource, Convert.ToUInt32("0x" + hexValue, 16)), true, GetImagePathFromID((Utilities.turn2bytes(hexValue)), itemSource, Convert.ToUInt32("0x" + Utilities.translateVariationValueBack(front), 16)), SelectedItem.getFlag1(), SelectedItem.getFlag2());
            }
            else if (ItemAttr.hasFenceWithVariation(IntId))  // Fence Variation
            {
                SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource, Convert.ToUInt32("0x" + front, 16)), true, "", SelectedItem.getFlag1(), SelectedItem.getFlag2());

                if (selection != null)
                {
                    selection.receiveID(IDTextbox.Text, languageSetting, Utilities.precedingZeros(hexValue, 8));
                }
            }
            else
            {
                if (ItemAttr.hasGenetics(Convert.ToUInt16("0x" + IDTextbox.Text, 16)))
                {
                    string value = AmountOrCountTextbox.Text;
                    int length = value.Length;
                    string firstByte;
                    string secondByte;
                    if (length < 2)
                    {
                        firstByte = "0";
                        secondByte = value;
                    }
                    else
                    {
                        firstByte = value.Substring(length - 2, 1);
                        secondByte = value.Substring(length - 1, 1);
                    }

                    setGeneComboBox(firstByte, secondByte);
                    GenePanel.Visible = true;
                }

                SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource, Convert.ToUInt32("0x" + hexValue, 16)), true, "", SelectedItem.getFlag1(), SelectedItem.getFlag2());

                if (selection != null)
                {
                    selection.receiveID(IDTextbox.Text, languageSetting, Utilities.precedingZeros(hexValue, 8));
                }
            }
            updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
        }

        private void IDTextbox_KeyUp(object sender, KeyEventArgs e)
        {
            if (IDTextbox.Text == "" | AmountOrCountTextbox.Text == "")
                return;

            string hexValue = "00000000";
            if (HexModeButton.Tag.ToString() == "Normal")
            {
                int decValue = Convert.ToInt32(AmountOrCountTextbox.Text) - 1;
                if (decValue >= 0)
                    hexValue = Utilities.precedingZeros(decValue.ToString("X"), 8);
            }
            else
                hexValue = Utilities.precedingZeros(AmountOrCountTextbox.Text, 8);

            if (IDTextbox.Text == "")
                return;

            UInt16 IntId = Convert.ToUInt16("0x" + IDTextbox.Text, 16);

            string front = Utilities.precedingZeros(hexValue, 8).Substring(0, 4);
            string back = Utilities.precedingZeros(hexValue, 8).Substring(4, 4);

            if (IDTextbox.Text == "16A2") //recipe
            {
                SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(hexValue), recipeSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(hexValue), recipeSource), true, "", SelectedItem.getFlag1(), SelectedItem.getFlag2());
            }
            else if (IDTextbox.Text == "315A" || IDTextbox.Text == "1618" || IDTextbox.Text == "342F") // Wall-Mounted
            {
                SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource, Convert.ToUInt32("0x" + hexValue, 16)), true, GetImagePathFromID((Utilities.turn2bytes(hexValue)), itemSource, Convert.ToUInt32("0x" + Utilities.translateVariationValueBack(front), 16)), SelectedItem.getFlag1(), SelectedItem.getFlag2());
            }
            else if (ItemAttr.hasFenceWithVariation(IntId))  // Fence Variation
            {
                SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource, Convert.ToUInt32("0x" + front, 16)), true, "", SelectedItem.getFlag1(), SelectedItem.getFlag2());

                if (selection != null)
                {
                    selection.receiveID(IDTextbox.Text, languageSetting, Utilities.precedingZeros(hexValue, 8));
                }
            }
            else
            {
                if (ItemAttr.hasGenetics(Convert.ToUInt16("0x" + IDTextbox.Text, 16)))
                {
                    string value = AmountOrCountTextbox.Text;
                    int length = value.Length;
                    string firstByte;
                    string secondByte;
                    if (length < 2)
                    {
                        firstByte = "0";
                        secondByte = value;
                    }
                    else
                    {
                        firstByte = value.Substring(length - 2, 1);
                        secondByte = value.Substring(length - 1, 1);
                    }

                    setGeneComboBox(firstByte, secondByte);
                    GenePanel.Visible = true;
                }

                SelectedItem.setup(GetNameFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource), Convert.ToUInt16("0x" + IDTextbox.Text, 16), Convert.ToUInt32("0x" + hexValue, 16), GetImagePathFromID(Utilities.turn2bytes(IDTextbox.Text), itemSource, Convert.ToUInt32("0x" + hexValue, 16)), true, "", SelectedItem.getFlag1(), SelectedItem.getFlag2());

                if (selection != null)
                {
                    selection.receiveID(IDTextbox.Text, languageSetting, Utilities.precedingZeros(hexValue, 8));
                }
            }
            updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
        }

        private void VersionButton_Click(object sender, EventArgs e)
        {
            MyMessageBox.Show(Utilities.CheckSysBotBase(socket, usb), "Sys-botbase Version");
        }

        private void CheckStateButton_Click(object sender, EventArgs e)
        {
            Thread stateThread = new Thread(delegate () { trystate(); });
            stateThread.Start();
        }
        private void trystate()
        {
            do
            {
                Debug.Print(teleport.GetOverworldState().ToString());
                Debug.Print(teleport.GetLocationState().ToString());
                Thread.Sleep(2000);
            } while (true);
        }

        public void KeyboardKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode.ToString() == "F2" || e.KeyCode.ToString() == "Insert")
            {
                if (selectedButton == null & (socket != null))
                {
                    int firstSlot = findEmpty();
                    if (firstSlot > 0)
                    {
                        selectedSlot = firstSlot;
                        updateSlot(firstSlot);
                    }
                }

                if (currentPanel == ItemModePanel)
                {
                    NormalItemSpawn();
                }
                else if (currentPanel == RecipeModePanel)
                {
                    RecipeSpawn();
                }
                else if (currentPanel == FlowerModePanel)
                {
                    FlowerSpawn();
                }

                int nextSlot = findEmpty();
                if (nextSlot > 0)
                {
                    selectedSlot = nextSlot;
                    updateSlot(nextSlot);
                }
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
            else if (e.KeyCode.ToString() == "F1")
            {
                DeleteItem();
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
            else if (e.KeyCode.ToString() == "F3")
            {
                CopyItem(sender, e);
                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
            else if (e.KeyCode.ToString() == "End")
            {
                if (ItemGridView.CurrentRow == null)
                    return;
                if (currentPanel == ItemModePanel)
                {
                    if (ItemGridView.Rows.Count <= 0)
                    {
                        return;
                    }
                    if (ItemGridView.CurrentRow == null)
                    {
                        return;
                    }

                    if (ItemGridView.Rows.Count == 1)
                    {
                        lastRow = ItemGridView.Rows[ItemGridView.CurrentRow.Index];
                        ItemGridView.Rows[ItemGridView.CurrentRow.Index].Height = 160;

                        if (HexModeButton.Tag.ToString() == "Normal")
                        {
                            if (AmountOrCountTextbox.Text == "" || AmountOrCountTextbox.Text == "0")
                            {
                                AmountOrCountTextbox.Text = "1";
                            }
                        }
                        else
                        {
                            HexModeButton_Click(sender, e);
                        }

                        string hexValue = "0";
                        int decValue = int.Parse(AmountOrCountTextbox.Text) - 1;
                        if (decValue >= 0)
                            hexValue = decValue.ToString("X");

                        IDTextbox.Text = ItemGridView.Rows[ItemGridView.CurrentRow.Index].Cells["id"].Value.ToString();

                        SelectedItem.setup(ItemGridView.Rows[ItemGridView.CurrentRow.Index].Cells[languageSetting].Value.ToString(), Convert.ToUInt16("0x" + ItemGridView.Rows[ItemGridView.CurrentRow.Index].Cells["id"].Value.ToString(), 16), 0x0, GetImagePathFromID(ItemGridView.Rows[ItemGridView.CurrentRow.Index].Cells["id"].Value.ToString(), itemSource), true);
                        if (selection != null)
                        {
                            selection.receiveID(Utilities.precedingZeros(SelectedItem.fillItemID(), 4), languageSetting, Utilities.precedingZeros(hexValue, 8));
                        }
                        updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
                    }
                    else if (ItemGridView.CurrentRow.Index + 1 < ItemGridView.Rows.Count)
                    {
                        if (lastRow != null)
                        {
                            lastRow.Height = 22;
                        }
                        lastRow = ItemGridView.Rows[ItemGridView.CurrentRow.Index + 1];
                        ItemGridView.Rows[ItemGridView.CurrentRow.Index + 1].Height = 160;

                        if (HexModeButton.Tag.ToString() == "Normal")
                        {
                            if (AmountOrCountTextbox.Text == "" || AmountOrCountTextbox.Text == "0")
                            {
                                AmountOrCountTextbox.Text = "1";
                            }
                        }
                        else
                        {
                            HexModeButton_Click(sender, e);
                            //AmountOrCountTextbox.Text = "1";
                        }

                        string hexValue = "0";
                        int decValue = int.Parse(AmountOrCountTextbox.Text) - 1;
                        if (decValue >= 0)
                            hexValue = decValue.ToString("X");

                        IDTextbox.Text = ItemGridView.Rows[ItemGridView.CurrentRow.Index + 1].Cells["id"].Value.ToString();

                        SelectedItem.setup(ItemGridView.Rows[ItemGridView.CurrentRow.Index + 1].Cells[languageSetting].Value.ToString(), Convert.ToUInt16("0x" + ItemGridView.Rows[ItemGridView.CurrentRow.Index + 1].Cells["id"].Value.ToString(), 16), 0x0, GetImagePathFromID(ItemGridView.Rows[ItemGridView.CurrentRow.Index + 1].Cells["id"].Value.ToString(), itemSource), true);
                        if (selection != null)
                        {
                            selection.receiveID(Utilities.precedingZeros(SelectedItem.fillItemID(), 4), languageSetting, Utilities.precedingZeros(hexValue, 8));
                        }
                        updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());

                        ItemGridView.CurrentCell = ItemGridView.Rows[ItemGridView.CurrentRow.Index + 1].Cells[languageSetting];

                        //Debug.Print(ItemGridView.CurrentRow.Index.ToString());
                    }
                }
                else if (currentPanel == RecipeModePanel)
                {
                    if (RecipeGridView.Rows.Count <= 0)
                    {
                        return;
                    }
                    if (RecipeGridView.CurrentRow == null)
                    {
                        return;
                    }

                    if (RecipeGridView.Rows.Count == 1)
                    {
                        recipelastRow = RecipeGridView.Rows[RecipeGridView.CurrentRow.Index];
                        RecipeGridView.Rows[RecipeGridView.CurrentRow.Index].Height = 160;

                        RecipeIDTextbox.Text = RecipeGridView.Rows[RecipeGridView.CurrentRow.Index].Cells["id"].Value.ToString();

                        SelectedItem.setup(RecipeGridView.Rows[RecipeGridView.CurrentRow.Index].Cells[languageSetting].Value.ToString(), 0x16A2, Convert.ToUInt32("0x" + RecipeGridView.Rows[RecipeGridView.CurrentRow.Index].Cells["id"].Value.ToString(), 16), GetImagePathFromID(RecipeGridView.Rows[RecipeGridView.CurrentRow.Index].Cells["id"].Value.ToString(), recipeSource), true);
                        updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
                    }
                    else if (RecipeGridView.CurrentRow.Index + 1 < RecipeGridView.Rows.Count)
                    {
                        if (recipelastRow != null)
                        {
                            recipelastRow.Height = 22;
                        }

                        recipelastRow = RecipeGridView.Rows[RecipeGridView.CurrentRow.Index + 1];
                        RecipeGridView.Rows[RecipeGridView.CurrentRow.Index + 1].Height = 160;

                        RecipeIDTextbox.Text = RecipeGridView.Rows[RecipeGridView.CurrentRow.Index + 1].Cells["id"].Value.ToString();

                        SelectedItem.setup(RecipeGridView.Rows[RecipeGridView.CurrentRow.Index + 1].Cells[languageSetting].Value.ToString(), 0x16A2, Convert.ToUInt32("0x" + RecipeGridView.Rows[RecipeGridView.CurrentRow.Index + 1].Cells["id"].Value.ToString(), 16), GetImagePathFromID(RecipeGridView.Rows[RecipeGridView.CurrentRow.Index + 1].Cells["id"].Value.ToString(), recipeSource), true);
                        updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());

                        RecipeGridView.CurrentCell = RecipeGridView.Rows[RecipeGridView.CurrentRow.Index + 1].Cells[languageSetting];
                    }
                }
                else if (currentPanel == FlowerModePanel)
                {

                }
            }
            else if (e.KeyCode.ToString() == "Home")
            {
                if (currentPanel == ItemModePanel)
                {
                    if (ItemGridView.Rows.Count <= 0)
                    {
                        return;
                    }
                    if (ItemGridView.CurrentRow == null)
                    {
                        return;
                    }

                    if (ItemGridView.Rows.Count == 1)
                    {
                        lastRow = ItemGridView.Rows[ItemGridView.CurrentRow.Index];
                        ItemGridView.Rows[ItemGridView.CurrentRow.Index].Height = 160;

                        if (HexModeButton.Tag.ToString() == "Normal")
                        {
                            if (AmountOrCountTextbox.Text == "" || AmountOrCountTextbox.Text == "0")
                            {
                                AmountOrCountTextbox.Text = "1";
                            }
                        }
                        else
                        {
                            HexModeButton_Click(sender, e);
                            //AmountOrCountTextbox.Text = "1";
                        }

                        string hexValue = "0";
                        int decValue = int.Parse(AmountOrCountTextbox.Text) - 1;
                        if (decValue >= 0)
                            hexValue = decValue.ToString("X");

                        IDTextbox.Text = ItemGridView.Rows[ItemGridView.CurrentRow.Index].Cells["id"].Value.ToString();

                        SelectedItem.setup(ItemGridView.Rows[ItemGridView.CurrentRow.Index].Cells[languageSetting].Value.ToString(), Convert.ToUInt16("0x" + ItemGridView.Rows[ItemGridView.CurrentRow.Index].Cells["id"].Value.ToString(), 16), 0x0, GetImagePathFromID(ItemGridView.Rows[ItemGridView.CurrentRow.Index].Cells["id"].Value.ToString(), itemSource), true);
                        if (selection != null)
                        {
                            selection.receiveID(Utilities.precedingZeros(SelectedItem.fillItemID(), 4), languageSetting, Utilities.precedingZeros(hexValue, 8));
                        }
                        updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
                    }
                    else if (ItemGridView.CurrentRow.Index > 0)
                    {
                        if (lastRow != null)
                        {
                            lastRow.Height = 22;
                        }

                        lastRow = ItemGridView.Rows[ItemGridView.CurrentRow.Index - 1];
                        ItemGridView.Rows[ItemGridView.CurrentRow.Index - 1].Height = 160;

                        if (HexModeButton.Tag.ToString() == "Normal")
                        {
                            if (AmountOrCountTextbox.Text == "" || AmountOrCountTextbox.Text == "0")
                            {
                                AmountOrCountTextbox.Text = "1";
                            }
                        }
                        else
                        {
                            HexModeButton_Click(sender, e);
                            //AmountOrCountTextbox.Text = "1";
                        }

                        string hexValue = "0";
                        int decValue = int.Parse(AmountOrCountTextbox.Text) - 1;
                        if (decValue >= 0)
                            hexValue = decValue.ToString("X");

                        IDTextbox.Text = ItemGridView.Rows[ItemGridView.CurrentRow.Index - 1].Cells["id"].Value.ToString();

                        SelectedItem.setup(ItemGridView.Rows[ItemGridView.CurrentRow.Index - 1].Cells[languageSetting].Value.ToString(), Convert.ToUInt16("0x" + ItemGridView.Rows[ItemGridView.CurrentRow.Index - 1].Cells["id"].Value.ToString(), 16), 0x0, GetImagePathFromID(ItemGridView.Rows[ItemGridView.CurrentRow.Index - 1].Cells["id"].Value.ToString(), itemSource), true);
                        if (selection != null)
                        {
                            selection.receiveID(Utilities.precedingZeros(SelectedItem.fillItemID(), 4), languageSetting, Utilities.precedingZeros(hexValue, 8));
                        }
                        updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());

                        ItemGridView.CurrentCell = ItemGridView.Rows[ItemGridView.CurrentRow.Index - 1].Cells[languageSetting];
                    }
                }
                else if (currentPanel == RecipeModePanel)
                {
                    if (RecipeGridView.Rows.Count <= 0)
                    {
                        return;
                    }
                    if (RecipeGridView.CurrentRow == null)
                    {
                        return;
                    }

                    if (RecipeGridView.Rows.Count == 1)
                    {
                        recipelastRow = RecipeGridView.Rows[RecipeGridView.CurrentRow.Index];
                        RecipeGridView.Rows[RecipeGridView.CurrentRow.Index].Height = 160;

                        RecipeIDTextbox.Text = RecipeGridView.Rows[RecipeGridView.CurrentRow.Index].Cells["id"].Value.ToString();

                        SelectedItem.setup(RecipeGridView.Rows[RecipeGridView.CurrentRow.Index].Cells[languageSetting].Value.ToString(), 0x16A2, Convert.ToUInt32("0x" + RecipeGridView.Rows[RecipeGridView.CurrentRow.Index].Cells["id"].Value.ToString(), 16), GetImagePathFromID(RecipeGridView.Rows[RecipeGridView.CurrentRow.Index].Cells["id"].Value.ToString(), recipeSource), true);
                        updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());
                    }
                    else if (RecipeGridView.CurrentRow.Index > 0)
                    {
                        if (recipelastRow != null)
                        {
                            recipelastRow.Height = 22;
                        }

                        recipelastRow = RecipeGridView.Rows[RecipeGridView.CurrentRow.Index - 1];
                        RecipeGridView.Rows[RecipeGridView.CurrentRow.Index - 1].Height = 160;

                        RecipeIDTextbox.Text = RecipeGridView.Rows[RecipeGridView.CurrentRow.Index - 1].Cells["id"].Value.ToString();

                        SelectedItem.setup(RecipeGridView.Rows[RecipeGridView.CurrentRow.Index - 1].Cells[languageSetting].Value.ToString(), 0x16A2, Convert.ToUInt32("0x" + RecipeGridView.Rows[RecipeGridView.CurrentRow.Index - 1].Cells["id"].Value.ToString(), 16), GetImagePathFromID(RecipeGridView.Rows[RecipeGridView.CurrentRow.Index - 1].Cells["id"].Value.ToString(), recipeSource), true);
                        updateSelectedItemInfo(SelectedItem.displayItemName(), SelectedItem.displayItemID(), SelectedItem.displayItemData());

                        RecipeGridView.CurrentCell = RecipeGridView.Rows[RecipeGridView.CurrentRow.Index - 1].Cells[languageSetting];
                    }
                }
                else if (currentPanel == FlowerModePanel)
                {

                }
            }
        }

        private void Converttocheat_Click(object sender, EventArgs e)
        {
            StringBuilder cheatmaker = new StringBuilder();

            string cheatHead = "08100000 ";

            string[] offsets = new string[40];
            for (int i = 0; i < 40; i++)
            {
                offsets[i] = cheatHead + Utilities.GetItemSlotUIntAddress(i + 1).ToString("X");
            }

            try
            {
                cheatmaker.AppendLine("\n[Your Cheat Name Here]");

                inventorySlot[] SlotPointer = new inventorySlot[40];
                foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                {
                    int slotId = int.Parse(btn.Tag.ToString());
                    SlotPointer[slotId - 1] = btn;
                }
                for (int i = 0; i < SlotPointer.Length; i++)
                {

                    string first = Utilities.precedingZeros(SlotPointer[i].getFlag1() + SlotPointer[i].getFlag2() + Utilities.precedingZeros(SlotPointer[i].fillItemID(), 4), 8);
                    string second = Utilities.precedingZeros(SlotPointer[i].fillItemData(), 8);

                    cheatmaker.AppendLine(offsets[i] + " " + second + " " + first);

                    Debug.Print(first + " " + second + " " + SlotPointer[i].getFlag1() + " " + SlotPointer[i].getFlag2() + " " + SlotPointer[i].fillItemID());
                }

                //save to your own filename :)   
                SaveFileDialog file = new SaveFileDialog()
                {
                    Filter = "Cheat Text (*.txt)|*.txt",
                    //FileName = "items.nhi",
                };

                Configuration config = ConfigurationManager.OpenExeConfiguration(Application.ExecutablePath.Replace(".exe", ".dll"));

                string savepath;

                if (config.AppSettings.Settings["LastSave"].Value.Equals(string.Empty))
                    savepath = Directory.GetCurrentDirectory() + @"\save";
                else
                    savepath = config.AppSettings.Settings["LastSave"].Value;

                //Debug.Print(savepath);
                if (Directory.Exists(savepath))
                {
                    file.InitialDirectory = savepath;
                }
                else
                {
                    file.InitialDirectory = @"C:\";
                }

                if (file.ShowDialog() != DialogResult.OK)
                    return;

                string[] temp = file.FileName.Split('\\');
                string path = "";
                for (int i = 0; i < temp.Length - 1; i++)
                    path = path + temp[i] + "\\";

                config.AppSettings.Settings["LastSave"].Value = path;
                config.Save(ConfigurationSaveMode.Minimal);


                //write to cheat//  
                File.WriteAllText(file.FileName, cheatmaker.ToString());

                if (sound)
                    System.Media.SystemSounds.Asterisk.Play();
            }
            catch (Exception ex)
            {
                MyLog.logEvent("MainForm", "Convert to Cheat txt: " + ex.Message.ToString());
                MyMessageBox.Show(ex.Message.ToString(), "Convert to Cheat Crashed");
            }
        }

        private void PeekButton_Click(object sender, EventArgs e)
        {
            var Address = Convert.ToUInt32(DebugAddress.Text, 16);
            byte[] AddressBank = Utilities.peekAddress(socket, null, Address, 256);

            byte[] firstBytes = new byte[4];
            byte[] secondBytes = new byte[4];
            byte[] thirdBytes = new byte[4];
            byte[] fourthBytes = new byte[4];

            byte[] firstFullResult = new byte[32];
            byte[] secondFullResult = new byte[32];
            byte[] thirdFullResult = new byte[32];
            byte[] fourthFullResult = new byte[32];
            byte[] fifthFullResult = new byte[32];

            Buffer.BlockCopy(AddressBank, 0x0, firstBytes, 0x0, 0x4);
            Buffer.BlockCopy(AddressBank, 0x4, secondBytes, 0x0, 0x4);
            Buffer.BlockCopy(AddressBank, 0x8, thirdBytes, 0x0, 0x4);
            Buffer.BlockCopy(AddressBank, 0xC, fourthBytes, 0x0, 0x4);

            Buffer.BlockCopy(AddressBank, 0x0, firstFullResult, 0x0, 0x20);
            Buffer.BlockCopy(AddressBank, 0x20, secondFullResult, 0x0, 0x20);
            Buffer.BlockCopy(AddressBank, 0x40, thirdFullResult, 0x0, 0x20);
            Buffer.BlockCopy(AddressBank, 0x60, fourthFullResult, 0x0, 0x20);
            Buffer.BlockCopy(AddressBank, 0x80, fifthFullResult, 0x0, 0x20);

            string firstResult = Utilities.ByteToHexString(firstBytes);
            string secondResult = Utilities.ByteToHexString(secondBytes);
            string thirdResult = Utilities.ByteToHexString(thirdBytes);
            string fourthResult = Utilities.ByteToHexString(fourthBytes);

            string FullResult1 = Utilities.ByteToHexString(firstFullResult);
            string FullResult2 = Utilities.ByteToHexString(secondFullResult);
            string FullResult3 = Utilities.ByteToHexString(thirdFullResult);
            string FullResult4 = Utilities.ByteToHexString(fourthFullResult);
            string FullResult5 = Utilities.ByteToHexString(fifthFullResult);

            PeekResult1.Text = Utilities.flip(firstResult);
            PeekResult2.Text = Utilities.flip(secondResult);
            PeekResult3.Text = Utilities.flip(thirdResult);
            PeekResult4.Text = Utilities.flip(fourthResult);

            FullPeekResult1.Text = FullResult1.Insert(56, " ").Insert(48, " ").Insert(40, " ").Insert(32, " ").Insert(24, " ").Insert(16, " ").Insert(8, " ");
            FullPeekResult2.Text = FullResult2.Insert(56, " ").Insert(48, " ").Insert(40, " ").Insert(32, " ").Insert(24, " ").Insert(16, " ").Insert(8, " ");
            FullPeekResult3.Text = FullResult3.Insert(56, " ").Insert(48, " ").Insert(40, " ").Insert(32, " ").Insert(24, " ").Insert(16, " ").Insert(8, " ");
            FullPeekResult4.Text = FullResult4.Insert(56, " ").Insert(48, " ").Insert(40, " ").Insert(32, " ").Insert(24, " ").Insert(16, " ").Insert(8, " ");
            FullPeekResult5.Text = FullResult5.Insert(56, " ").Insert(48, " ").Insert(40, " ").Insert(32, " ").Insert(24, " ").Insert(16, " ").Insert(8, " ");
        }

        private void PokeButton_Click(object sender, EventArgs e)
        {
            Utilities.pokeAddress(socket, null, DebugAddress.Text, DebugValue.Text);
        }

        private void UnhideButton_Click(object sender, EventArgs e)
        {
            if (currentPanel == ItemModePanel)
            {
                ItemGridView.Columns["id"].Visible = true;
                ItemGridView.Columns["iName"].Visible = true;
                ItemGridView.Columns["color"].Visible = true;
                ItemGridView.Columns["size"].Visible = true;
            }
            else if (currentPanel == RecipeModePanel)
            {
                RecipeGridView.Columns["id"].Visible = true;
                RecipeGridView.Columns["iName"].Visible = true;
            }
            else
                return;
        }

        private void USBConnectionButton_Click(object sender, EventArgs e)
        {
            if (USBConnectionButton.Tag.ToString() == "connect")
            {
                usb = new USBBot();
                if (usb.Connect())
                {
                    MyLog.logEvent("MainForm", "Connection Succeeded : USB");

                    this.RefreshButton.Visible = true;
                    this.PlayerInventorySelector.Visible = true;

                    this.InventoryAutoRefreshToggle.Visible = true;
                    this.AutoRefreshLabel.Visible = true;

                    this.OtherTabButton.Visible = true;
                    this.CritterTabButton.Visible = true;
                    this.VillagerTabButton.Visible = true;

                    this.WrapSelector.SelectedIndex = 0;

                    this.USBConnectionButton.Text = "Disconnect";
                    this.USBConnectionButton.Tag = "Disconnect";
                    this.StartConnectionButton.Visible = false;
                    this.SettingButton.Visible = false;

                    //this.BulldozerButton.Visible = true;

                    offline = false;

                    CurrentPlayerIndex = updateDropdownBox();

                    PlayerInventorySelector.SelectedIndex = CurrentPlayerIndex;
                    PlayerInventorySelectorOther.SelectedIndex = CurrentPlayerIndex;
                    this.Text = this.Text + UpdateTownID() + " | [Connected via USB]";

                    setEatButton();
                    UpdateTurnipPrices();
                    readWeatherSeed();

                    this.IPAddressInputBox.Visible = false;
                    this.IPAddressInputBackground.Visible = false;

                    currentGridView = InsectGridView;
                    LoadGridView(InsectAppearParam, InsectGridView, ref insectRate, Utilities.InsectDataSize, Utilities.InsectNumRecords);
                    LoadGridView(FishRiverAppearParam, RiverFishGridView, ref riverFishRate, Utilities.FishDataSize, Utilities.FishRiverNumRecords, 1);
                    LoadGridView(FishSeaAppearParam, SeaFishGridView, ref seaFishRate, Utilities.FishDataSize, Utilities.FishSeaNumRecords, 1);
                    LoadGridView(CreatureSeaAppearParam, SeaCreatureGridView, ref seaCreatureRate, Utilities.SeaCreatureDataSize, Utilities.SeaCreatureNumRecords, 1);
                }
                else
                {
                    MyLog.logEvent("MainForm", "Connection Failed : USB");
                    usb = null;
                }
            }
            else
            {
                usb.Disconnect();

                foreach (inventorySlot btn in this.InventoryPanel.Controls.OfType<inventorySlot>())
                {
                    btn.reset();
                }

                this.RefreshButton.Visible = false;
                this.PlayerInventorySelector.Visible = false;

                this.InventoryAutoRefreshToggle.Visible = false;
                this.AutoRefreshLabel.Visible = false;

                this.OtherTabButton.Visible = false;
                this.CritterTabButton.Visible = false;
                this.VillagerTabButton.Visible = false;

                this.SettingButton.Visible = true;

                this.IPAddressInputBox.Visible = true;
                this.IPAddressInputBackground.Visible = true;

                InventoryTabButton_Click(sender, e);
                CleanVillagerPage();

                this.USBConnectionButton.Text = "USB";
                this.USBConnectionButton.Tag = "connect";
                this.Text = version;
            }
        }

        int startID = 0x38c8;

        private void FillButton_Click(object sender, EventArgs e)
        {
            string Bank1 = "";
            string Bank2 = "";
            int counter = 0;

            do
            {
                string id = Utilities.precedingZeros(startID.ToString("X"), 4);
                if (itemExist(id))
                {

                }
                else
                {
                    string first = Utilities.flip(Utilities.precedingZeros("00" + "00" + id, 8));
                    string second = "00000000";
                    if (counter < 20)
                        Bank1 = Bank1 + first + second;
                    else
                        Bank2 = Bank2 + first + second;
                    counter++;
                }

                startID++;

            } while (counter < 40);

            byte[] Inventory1 = new byte[160];
            byte[] Inventory2 = new byte[160];

            for (int i = 0; i < Bank1.Length / 2 - 1; i++)
            {
                string tempStr1 = String.Concat(Bank1[(i * 2)].ToString(), Bank1[((i * 2) + 1)].ToString());
                string tempStr2 = String.Concat(Bank2[(i * 2)].ToString(), Bank2[((i * 2) + 1)].ToString());
                //Debug.Print(i.ToString() + " " + data);
                Inventory1[i] = Convert.ToByte(tempStr1, 16);
                Inventory2[i] = Convert.ToByte(tempStr2, 16);
            }

            Utilities.OverwriteAll(socket, usb, Inventory1, Inventory2, ref counter);

            UpdateInventory();
            if (sound)
                System.Media.SystemSounds.Asterisk.Play();
        }

        private bool itemExist(string id)
        {
            if (itemSource == null)
            {
                return false;
            }

            DataRow row = itemSource.Rows.Find(id);

            if (row == null)
                return false;
            else
                return true;
        }
    }
}
