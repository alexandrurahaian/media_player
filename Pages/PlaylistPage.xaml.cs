using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Media_Player.Scripts;
using Microsoft.Win32;

namespace Media_Player.Objects
{
    /// <summary>
    /// Interaction logic for PlaylistPage.xaml
    /// </summary>
    public partial class PlaylistPage : Page
    {
        private ItemCollection ?previously_extracted = null;
        private bool opened_edit = false;
        private void GenerateFiles(string[] file_paths)
        {
            int index = 0;
            foreach (string f_path in file_paths)
            {
                string file_name = System.IO.Path.GetFileName(f_path);
                ListViewItem item = new ListViewItem();
                item.Name = $"Item{index}";
                item.Content = file_name;
                item.Tag = f_path;
                opened_files_list.Items.Add(item);
                index++;
            }
        }

        public void GeneratePlaylist(PlaylistObject playlist)
        {
            
            for (int i = 0; i < current_playlists.Items.Count; i++)
            {
                ListViewItem ?fetched_item = current_playlists.Items[i] as ListViewItem;
                if (fetched_item != null && fetched_item.Content.ToString().Contains(playlist.name))
                {
                    current_playlists.Items.RemoveAt(i);
                    break;
                } 
            }

            ListViewItem playlist_item = new ListViewItem();
            playlist_item.Name = playlist.name;
            
            playlist_item.Content = $"{playlist.name} --- Media items: {playlist.playlist_items.Count}";
            current_playlists.Items.Add(playlist_item);
        }

        public PlaylistPage(ItemCollection selected_items)
        {
            InitializeComponent();
            this.DataContext = this;
            previously_extracted = selected_items;
            foreach (ListViewItem item in selected_items)
            {
                ListViewItem new_item = new ListViewItem();
                new_item.Tag = item.Tag;
                new_item.Name = item.Name;
                new_item.Content = item.Content;
                opened_files_list.Items.Add(new_item);
            }
            count_display.Text = $"Selected items: {selected_items.Count}";

            List<PlaylistObject> ?loaded_playlists = PlaylistHandler.LoadPlaylists();
            if (loaded_playlists != null)
            {
                foreach (PlaylistObject playlist in loaded_playlists)
                {
                    GeneratePlaylist(playlist);
                }
            }
        }

        private void edit_selected_playlist_btn_Click(object sender, RoutedEventArgs e)
        {
            ListViewItem? selected_item = current_playlists.SelectedItem as ListViewItem;
            if (selected_item != null)
            {
                string? selected_name = selected_item.Content.ToString().Split(" --- ")[0];
                if (selected_name != null && MessageBox.Show($"Are you sure you wish to edit {selected_name}?", $"Editing {selected_name}", MessageBoxButton.YesNoCancel, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    PlaylistEditPage edit_page = new PlaylistEditPage(selected_name, opened_files_list.Items, this);
                    edit_page.ShowDialog();
                }
            }
            else
            {
                MessageBox.Show("Please select a playlist first!");
            }
        }

        private void remove_selected_playlist_btn_Click(object sender, RoutedEventArgs e)
        {
            ListViewItem? selected_item = current_playlists.SelectedItem as ListViewItem;
            if (selected_item != null)
            {
                string ?selected_name = selected_item.Content.ToString().Split(" --- ")[0];
                if (selected_name != null && MessageBox.Show($"Are you sure you wish to remove the following playlist: {selected_name}? This is permanent!", $"Deleting {selected_name}", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    current_playlists.Items.Remove(current_playlists.SelectedItem);
                    PlaylistHandler.DeletePlaylist(selected_name);
                    StatisticsObject.TotalPlaylists = Math.Clamp(--StatisticsObject.TotalPlaylists, 0, int.MaxValue);
                }
            }
            else
            {
                MessageBox.Show("Please select a playlist first!");
            }
        }

        private void load_selected_playlist_btn_Click(object sender, RoutedEventArgs e)
        {
            ListViewItem ?selected_item = current_playlists.SelectedItem as ListViewItem;
            if (selected_item != null)
            {
                string ?selected_name = selected_item.Content.ToString();
                if (selected_name != null)
                {
                    string[] selected = selected_name.Split(" --- ");
                    selected_name = selected[0];
                    if (MessageBox.Show($"Are you sure you wish to load the following playlist: {selected_name}?", $"Loading {selected_name}", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                    {
                        return;
                    }

                    PlaylistObject? playlist = PlaylistHandler.LoadPlaylist(selected_name);
                    if (playlist != null)
                    {
                        opened_files_list.Items.Clear();
                        List<string> extracted_playlist_item_paths = playlist.playlist_items;
                        int count = 0;
                        int invalid_paths_found = 0;
                        string[] invalid_paths = new string[extracted_playlist_item_paths.Count];
                        foreach (string file_path in extracted_playlist_item_paths)
                        {
                            if (System.IO.Path.Exists(file_path))
                            {
                                string fileName = System.IO.Path.GetFileName(file_path);
                                ListViewItem item = new ListViewItem();
                                item.Name = $"Item{count}";
                                item.Content = fileName;
                                item.Tag = file_path;
                                opened_files_list.Items.Add(item);
                            }
                            else
                            {
                                invalid_paths[invalid_paths_found] = file_path;
                                invalid_paths_found++;
                            }
                        }
                        if (invalid_paths_found > 0)
                        {
                            string main = string.Empty;
                            foreach (string file_path in invalid_paths)
                            {
                                main = $"{main}{file_path}\n";
                            }

                            if (MessageBox.Show($"ATTENTION! {invalid_paths_found} INVALID PATHS DETECTED:\n{main}\n\n\nWould you like to automatically remove them?", $"{invalid_paths_found} invalid paths found.", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                            {
                                foreach (string invalid_path in invalid_paths)
                                {
                                    if (extracted_playlist_item_paths.Contains(invalid_path))
                                    {
                                        extracted_playlist_item_paths.Remove(invalid_path);
                                    }
                                }
                                PlaylistObject new_obj = new PlaylistObject(playlist.name, extracted_playlist_item_paths, playlist.item_playcount);
                                PlaylistHandler.DeletePlaylist(playlist.name);
                                PlaylistHandler.SavePlaylist(new_obj);
                            }
                        }
                        MessageBox.Show($"Successfully loaded {playlist.name}!", $"Loaded {playlist.name}", MessageBoxButton.OK, MessageBoxImage.Information);

                    }
                    else
                    {
                        MessageBox.Show($"Failed to fetch {selected_name} from database!");
                    }
                }
                else
                {
                    MessageBox.Show($"Could not fetch name of selected item! Please try again!");
                }
            }
            else
            {
                MessageBox.Show("Please select a playlist first!");
            }
        }

        private void folder_file_radio_btn_Click(object sender, RoutedEventArgs e)
        {
            if (folder_file_radio_btn.IsChecked == true)
            {
                select_file_panel.Visibility = Visibility.Visible;
                opened_files_list.Items.Clear();
            }
        }

        private void use_opened_folder_radio_btn_Click(object sender, RoutedEventArgs e)
        {
            select_file_panel.Visibility = Visibility.Collapsed;
            opened_files_list.Items.Clear();
        }

        private string selected_mode = "file";
        private void select_files_btn_Click(object sender, RoutedEventArgs e)
        {
            if (selected_mode == "file")
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Title = "Select file/files";
                dialog.Multiselect = true;

                if (dialog.ShowDialog() == true)
                {
                    GenerateFiles(dialog.FileNames);
                    create_playlist_btn.IsEnabled = true;
                }
            }
            else if (selected_mode == "folder")
            {
                OpenFolderDialog dialog = new OpenFolderDialog();
                dialog.Title = "Select a folder";
                if (dialog.ShowDialog() == true)
                {
                    GenerateFiles(Directory.GetFiles(dialog.FolderName));
                    create_playlist_btn.IsEnabled = true;
                }
            }
        }

        private char[] illegal_name_chars = { '[', ']', '-', ' ' };

        private bool NameContainsIllegalChars(string name)
        {

            foreach (char c in name.ToCharArray())
                if (illegal_name_chars.Contains<char>(c)) return true;

            return false;
        }

        private void create_playlist_btn_Click(object sender, RoutedEventArgs e)
        {
            bool name_already_exists = PlaylistHandler.NameAlreadyExists(playlist_name_box.Text);

            if (NameContainsIllegalChars(playlist_name_box.Text))
            {
                MessageBox.Show($"Name contains illegal characters!", "Illegal characters found in name.", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!string.IsNullOrEmpty(playlist_name_box.Text) && !name_already_exists)
            {
                string name = playlist_name_box.Text;
                List<string> extracted_items = new List<string>();
                Dictionary<string, int> item_counts = new Dictionary<string, int>();

                int c = 0;
                try
                {
                    foreach (ListViewItem item in opened_files_list.Items)
                    {
                        extracted_items.Add(item.Tag.ToString());
                        item_counts.Add(item.Tag.ToString(), 0);
                    }
                }
                catch (Exception ex) { } 

                PlaylistObject new_object = new PlaylistObject(name, extracted_items, item_counts);
                PlaylistHandler.SavePlaylist(new_object);
                StatisticsObject.TotalPlaylists++;
                GeneratePlaylist(new_object);
            }
            else if (name_already_exists)
            {
                MessageBox.Show($"{playlist_name_box.Text} already exists! Please chose a different name.", $"Playlist {playlist_name_box.Text} already exists.", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void file_select_radio_btn_Click(object sender, RoutedEventArgs e)
        {
            if (file_select_radio_btn.IsChecked == true)
            {
                selected_mode = "file";
            }
        }

        private void folder_select_radio_btn_Click(object sender, RoutedEventArgs e)
        {
            if (folder_file_radio_btn.IsChecked == true)
            {
                selected_mode = "folder";
            }
        }

        private void select_all_btn_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show($"Are you sure you wish to select all ({opened_files_list.Items.Count}) items?", $"Selecting {opened_files_list.Items.Count}...", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                opened_files_list.SelectAll();
            }
        }

        private void deselect_all_btn_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show($"Are you sure you wish to deselect all ({opened_files_list.SelectedItems.Count}) items?", $"Deselecting {opened_files_list.SelectedItems.Count}...", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                opened_files_list.SelectedItems.Clear();
            }
        }

        private void playlist_name_box_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(playlist_name_box.Text)) {
                create_playlist_btn.IsEnabled = false;
                return;
            }

            if (use_opened_folder_radio_btn.IsChecked == true && opened_files_list.Items.Count > 0)
            {
                create_playlist_btn.IsEnabled = true;
            }
            else if ((use_opened_folder_radio_btn.IsChecked == true && opened_files_list.Items.Count <= 0))
            {
                create_playlist_btn.IsEnabled = false;
            }
        }

        private void opened_files_list_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            count_display.Text = $"Selected files: {opened_files_list.SelectedItems.Count}";
        }

        private void playlist_page_display_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.Delete) && opened_files_list.SelectedItems.Count > 0 && MessageBox.Show($"Are you sure you want to delete selected items?", "Deleting selected...", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                for (int i = opened_files_list.SelectedItems.Count - 1; i >= 0; i--)
                {
                    opened_files_list.Items.Remove(opened_files_list.SelectedItems[i]);
                }
            }
        }
    }
}
