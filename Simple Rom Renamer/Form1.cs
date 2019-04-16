using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Security.Cryptography;

namespace Simple_Rom_Renamer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            string[] datafiles = Directory.GetFiles("datafile");

            for (int i = 0; i < datafiles.Length; i++)
            {
                datafiles[i] = Path.GetFileName(datafiles[i]);
                comboBox1.Items.Add(datafiles[i]);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string datafile = comboBox1.Text;
            string path_to_datafile = Path.Combine("datafile", datafile);

            XmlDocument datafile_xml = new XmlDocument() { XmlResolver = null };
            datafile_xml.Load(path_to_datafile);

            XmlDocument datafile_miss_xml = new XmlDocument() { XmlResolver = null };
            string path_to_datafile_miss = Path.Combine("miss", datafile + ".miss.xml");

            if (File.Exists(path_to_datafile_miss))
            {
                datafile_miss_xml.Load(path_to_datafile_miss);
            }
            else
            {
                datafile_miss_xml.LoadXml("<datafile />");
                XmlNodeList games = datafile_xml.DocumentElement.SelectNodes("game");

                foreach(XmlNode game in games)
                {
                    XmlNode game_miss = datafile_miss_xml.ImportNode(game, true);
                    datafile_miss_xml.DocumentElement.AppendChild(game_miss);
                }

                datafile_miss_xml.Save(path_to_datafile_miss);
            }

            XmlDocument datafile_have_xml = new XmlDocument() { XmlResolver = null };
            string path_to_datafile_have = Path.Combine("have", datafile + ".have.xml");

            if (File.Exists(path_to_datafile_have))
            {
                datafile_have_xml.Load(path_to_datafile_have);
            }
            else
            {
                datafile_have_xml.LoadXml("<datafile />");

                datafile_have_xml.Save(path_to_datafile_have);
            }

            //XmlNodeList miss_games = datafile_miss_xml.DocumentElement.SelectNodes("game");

            UpdateTree(datafile_miss_xml, treeView1, groupBoxMiss);
            UpdateTree(datafile_have_xml, treeView2, groupBoxHave);

        }

        private void UpdateTree(XmlDocument datafile_miss_xml, TreeView treeView1, GroupBox groupBox1)
        {
            XmlNodeList miss_games = datafile_miss_xml.DocumentElement.SelectNodes("game");

            long total_size = 0;
            int total_roms = datafile_miss_xml.DocumentElement.SelectNodes("game/rom").Count;
            int total_games = datafile_miss_xml.DocumentElement.SelectNodes("game").Count;

            Invoke((MethodInvoker) delegate {
                    treeView1.Nodes.Clear();
                    foreach (XmlNode miss_game in miss_games)
                    {
                        string game_name = miss_game.Attributes["name"].Value;

                        TreeNode temp_node = new TreeNode(game_name);

                        XmlNodeList roms = miss_game.SelectNodes("rom");

                        foreach (XmlNode rom in roms)
                        {
                            string rom_name = rom.Attributes["name"].Value;

                            long rom_size = Convert.ToInt64(rom.Attributes["size"].Value);
                            total_size += rom_size;

                            temp_node.Nodes.Add(rom_name);
                        }
                        treeView1.Nodes.Add(temp_node);
                    }
                    treeView1.Sort();
                    if (groupBox1.Name.Contains("Miss"))
                    {
                        groupBox1.Text = string.Format("Missing: {0} GiB, {1} sets, {2} roms", Math.Round(((float)total_size / 1E+9), 3), total_games, total_roms);
                    }
                    else
                    {
                        groupBox1.Text = string.Format("Have: {0} GiB, {1} sets, {2} roms", Math.Round(((float)total_size / 1E+9), 3), total_games, total_roms);
                    }
                });
        }

        private void listBox1_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void listBox1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = ((string[])e.Data.GetData(DataFormats.FileDrop));

            backgroundWorker1.RunWorkerAsync(files);
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            XmlDocument datafile = new XmlDocument() { XmlResolver = null };

            string datafile_name = string.Empty;
            Invoke((MethodInvoker)delegate
            {
                datafile_name = comboBox1.Text;
            });

            if (datafile_name == string.Empty)
            {
                MessageBox.Show("Select Datafile");
                return;
            }

            if (textBox1.Text == string.Empty)
            {
                MessageBox.Show("Select Output Folder");
                return;
            }
            else
            {
                string path_to_datafile = Path.Combine("datafile", datafile_name);
                datafile.Load(path_to_datafile);
            }

            string[] files = (string[])e.Argument;

            for (int i = 0; i < files.Length; i++)
            {
                string file_name = new FileInfo(files[i]).Name;

                Invoke((MethodInvoker)delegate
                {
                    listBox1.Items.Add(string.Format("Processing: \"{0}\"", file_name));
                });

                string file_size = new FileInfo(files[i]).Length.ToString();
                string file_md5 = CalculateMD5(files[i]);

                Invoke((MethodInvoker)delegate
                {
                    listBox1.Items.Add(string.Format("MD5: \"{0}\"; Size: {1}", file_md5, file_size));
                });

                XmlNodeList roms = datafile.DocumentElement.SelectNodes(string.Format("game/rom[@md5=\"{0}\"]", file_md5));

                Invoke((MethodInvoker)delegate
                {
                    if (roms.Count == 0)
                    {
                        listBox1.Items.Add("Found: 0 matches");
                    }
                    else
                    {
                        string match = (roms.Count > 1) ? "matches" : "match";
                        listBox1.Items.Add(string.Format("Found: {0} {1}", roms.Count, match));
                    }
                });

                for (int j = 0; j < roms.Count; j++)
                {
                    string old_file = files[i];
                    string folder = roms[j].ParentNode.Attributes["name"].Value;
                    folder = Path.Combine(textBox1.Text, folder);
                    try { Directory.CreateDirectory(folder); }
                    catch { };
                    string new_file = Path.Combine(folder, roms[j].Attributes["name"].Value);

                    try
                    {
                        if ((j + 1) == roms.Count)
                        {
                            File.Move(old_file, new_file);
                        }
                        else
                        {
                            File.Copy(old_file, new_file);
                        }
                    }
                    catch { };

                    Stats(roms[j], datafile_name);
                }
            }
        }

        private void Stats(XmlNode rom, string datafile_name)
        {
            XmlDocument miss = new XmlDocument() { XmlResolver = null };
            XmlDocument have = new XmlDocument() { XmlResolver = null };

            string path_to_datafile_miss = Path.Combine("miss", datafile_name + ".miss.xml");
            string path_to_datafile_have = Path.Combine("have", datafile_name + ".have.xml");

            miss.Load(path_to_datafile_miss);
            have.Load(path_to_datafile_have);

            XmlNode game = rom.ParentNode;

            XmlNode game_to_remove_from = miss.DocumentElement.SelectSingleNode(string.Format("game[@name=\"{0}\"]", game.Attributes["name"].Value));

            XmlNode rom_to_remove = miss.ImportNode(rom, true);
            XmlNode rom_to_add = have.ImportNode(rom, true);

            game_to_remove_from.RemoveChild(game_to_remove_from.SelectSingleNode(string.Format("rom[@md5=\"{0}\"]", rom_to_remove.Attributes["md5"].Value)));

            if (game_to_remove_from.SelectNodes("rom").Count == 0) miss.DocumentElement.RemoveChild(game_to_remove_from);

            XmlNode game_to_add = have.DocumentElement.SelectSingleNode(string.Format("game[@name=\"{0}\"]", game.Attributes["name"].Value));

            if (game_to_add == null)
            {
                XmlElement name = have.CreateElement("game");
                name.SetAttribute("name", game.Attributes["name"].Value);
                name.AppendChild(rom_to_add);
                have.DocumentElement.AppendChild(name);
            }
            else
            {
                game_to_add.AppendChild(rom_to_add);
            }

            miss.Save(path_to_datafile_miss);
            have.Save(path_to_datafile_have);

            UpdateTree(miss, treeView1, groupBoxMiss);
            UpdateTree(have, treeView2, groupBoxHave);
        }

        private string CalculateMD5(string file)
        {
            MD5 hash = MD5.Create();

            using (BinaryReader br = new BinaryReader(new FileStream(file, FileMode.Open)))
            {
                long file_size = br.BaseStream.Length;

                while (file_size > 0)
                {
                    int block_size = ((file_size % 2048) == 0) ? 2048 : (int)(file_size % 2048);

                    byte[] temp_block = br.ReadBytes(block_size);

                    hash.TransformBlock(temp_block, 0, block_size, null, 0);

                    file_size -= block_size;
                }

                hash.TransformFinalBlock(new byte[0], 0, 0);
            }
            string md5 = BitConverter.ToString(hash.Hash).Replace("-", "").ToLower();
            hash.Dispose();
            return md5;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.ShowDialog();
            textBox1.Text = folderBrowserDialog1.SelectedPath;
        }
    }
}
