using Media_Player.Objects;
using Media_Player.Scripts;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace Media_Player
{
    /// <summary>
    /// Interaction logic for PlaylistEditPage.xaml
    /// </summary>
    public partial class PlaylistEditPage : Window
    {
        private List<string> unchecked_items = new List<string>();
        private List<string> new_additions_string = new List<string>();
        private PlaylistObject ?fetched_playlist = null;
        private PlaylistPage ?main_page = null;

        private bool HaveChangesBeenMade()
        {
            bool result = false;

            if (unchecked_items.Count > 0 || new_additions_string.Count > 0)
            {
                result = true;
            }

            return result;
        }

        //private object? GetItemWithName(string name, ListView target)
        //{
        //    foreach (var item in target.Items)
        //    {
        //        if (item is ListViewItem listview && listview.Tag?.ToString() == name)
        //        {
        //            return listview;
        //        }
        //        else if (item is CheckBox checkbox && checkbox.Tag?.ToString() == name)
        //        {
        //            return checkbox;
        //        }
        //    }
        //    return null;
        //}

        public PlaylistEditPage(string playlist_name, ItemCollection opened_file_coll, PlaylistPage sender)
        {
            InitializeComponent();
            PlaylistObject ?playlist = PlaylistHandler.LoadPlaylist(playlist_name);
            main_page = sender;
            int index = 0;
            if (playlist != null )
            {
                fetched_playlist = playlist;
                foreach (string playlist_item in playlist.playlist_items)
                {
                    CheckBox item = new CheckBox();
                    item.Tag = playlist_item;
                    item.Content = System.IO.Path.GetFileName(playlist_item);
                    item.Name = $"Box{index}";
                    item.IsChecked = true;
                    item.Click += Item_Checked;
                    item.Foreground = (Brush)Application.Current.Resources["ForegroundBrush"];
                    playlist_contents.Items.Add(item);
                    index++;
                }
                playlist_item_count.Text = $"Playlist items: {index}";
                

                foreach (CheckBox item in playlist_contents.Items)
                {
                    CheckBox n_item = new CheckBox();
                    n_item.Tag = item.Tag;
                    n_item.Name = item.Name;
                    n_item.Content = item.Content;
                    n_item.IsChecked = true;
                    n_item.IsEnabled = false;
                    n_item.Foreground = (Brush)Application.Current.Resources["ForegroundBrush"];
                    add_song_list.Items.Add(n_item);
                }

                foreach (ListViewItem item in opened_file_coll)
                {
                    if (!playlist.playlist_items.Contains(item.Tag) && !playlist_contents.Items.Contains(item.Tag))
                    {
                        CheckBox new_item = new CheckBox();
                        new_item.Tag = item.Tag;
                        new_item.Name = item.Name;
                        new_item.Content = item.Content;
                        new_item.Click += New_item_Click;
                        new_item.Foreground = (Brush)Application.Current.Resources["ForegroundBrush"];
                        add_song_list.Items.Add(new_item);
                    }
                }
            }
            else
            {
                this.Close();
            }
        }
        private void New_item_Click(object sender, RoutedEventArgs e)
        {
            CheckBox? check_box = sender as CheckBox;
            if (check_box != null)
            {
                string to_add_str = check_box.Tag.ToString();
                if (!new_additions_string.Contains(to_add_str) && check_box.IsChecked == true)
                {
                    new_additions_string.Add(check_box.Tag.ToString());
                }
                else if (new_additions_string.Contains(to_add_str) && check_box.IsChecked == false)
                {
                    new_additions_string.Remove(check_box.Tag.ToString());
                }
            }
        }

        private void Item_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox ?check_box = sender as CheckBox;
            if (check_box == null) return;
            if (check_box.IsChecked == false)
            {
                unchecked_items.Add(check_box.Tag.ToString());
                Debug.WriteLine(check_box.Tag.ToString());
            }
            else if (check_box.IsChecked == true && unchecked_items.Contains(check_box.Tag.ToString()))
                unchecked_items.Remove(check_box.Tag.ToString());
        }

        private void confirm_btn_Click(object sender, RoutedEventArgs e)
        {
            if (fetched_playlist != null)
            {
                bool did_edit = HaveChangesBeenMade();
                if(did_edit)
                {
                    string new_addition_str = string.Empty;
                    string removing_str = string.Empty;

                    foreach (string item in unchecked_items)
                    {
                        removing_str += $"{item}\n";
                    }

                    foreach (string item in new_additions_string)
                    {
                        new_addition_str += $"{item}\n";
                    }

                    string changes = $"""
                        ! CAUTION !
                        Detected the following changes:
                        Adding the following items:
                        {new_addition_str}
                        Removing the following items:
                        {removing_str}


                        Are you sure you wish to apply these edits? (This action CANNOT be reverted!)
                        """;

                    int amount_for_statistics = new_additions_string.Count - unchecked_items.Count;

                    if (MessageBox.Show(changes, "Confirming changes...", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        string name = fetched_playlist.name;
                        List<string> playlist_items = new List<string>();
                        playlist_items = fetched_playlist.playlist_items;
                        Dictionary<string, int> item_counts = fetched_playlist.item_playcount;
                        for (int i = 0; i <  playlist_items.Count; i++)
                        {
                            string item = playlist_items[i];
                            if (unchecked_items.Contains(item))
                            {
                                playlist_items.Remove(item);
                                item_counts.Remove(item);
                            }
                        }
                        for (int i = 0; i < add_song_list.Items.Count; i++)
                        {
                            CheckBox item = add_song_list.Items[i] as CheckBox;
                            if (item.IsChecked == true && item.IsEnabled == true && !playlist_items.Contains(item.Tag.ToString()))
                            {
                                playlist_items.Add(item.Tag.ToString());
                                item_counts.Add(item.Tag.ToString(), 0);
                            }
                        }
                        PlaylistObject edited_playlist = new PlaylistObject(name, playlist_items, item_counts);

                        PlaylistHandler.DeletePlaylist(name);
                        PlaylistHandler.SavePlaylist(edited_playlist);
                        main_page.GeneratePlaylist(edited_playlist);
                        StatisticsObject.TotalTracksInPlaylists = Math.Clamp(StatisticsObject.TotalTracksInPlaylists + amount_for_statistics, 0, int.MaxValue); // so it doesnt turn negative
                        MessageBox.Show($"Completed edit of {name}!", "Completed playlist editing", MessageBoxButton.OK, MessageBoxImage.Information);
                        this.Close();
                    }
                }
                else
                {
                    if (MessageBox.Show("No changes have been detected. Are you sure you wish to exit editing?", "No edits detected!", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                    {
                        this.Close();
                    }
                }
            }
        }

        private void change_paths_btn_Click(object sender, RoutedEventArgs e)
        {
            if (playlist_contents.SelectedItems.Count <= 0) MessageBox.Show("Please select atleast 1 media element!");
            if (fetched_playlist == null) return;

            string confirmation_str = "Are you sure you wish to change the paths of the following files:\n";
            foreach (CheckBox item in playlist_contents.SelectedItems)
            {
                confirmation_str += (item.Content + "\n");
            }
            if (MessageBox.Show(confirmation_str, "Replacing paths", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select a new folder";

            string ?folder_name = (ofd.ShowDialog() == true) ? ofd.FolderName : null;
            if (folder_name == null) return;

            foreach (CheckBox c_item in playlist_contents.SelectedItems)
            {
                string item = c_item.Content.ToString();

                for (int i = fetched_playlist.playlist_items.Count - 1; i >= 0; i--)
                {
                    string fetched = fetched_playlist.playlist_items[i];
                    if (fetched.Contains(item))
                    {
                        string replaced = System.IO.Path.Combine(folder_name, System.IO.Path.GetFileName(item));
                        fetched_playlist.playlist_items[i] = replaced;

                        string as_string = UtilityHandler.GeneratePlaylistItemCount(fetched_playlist.item_playcount);
                        as_string = as_string.Replace(fetched, replaced);
                        fetched_playlist.item_playcount = UtilityHandler.GeneratePlaylistItemCount(as_string);
                    }
                }
            }
            PlaylistObject edited_playlist = new PlaylistObject(fetched_playlist.name, fetched_playlist.playlist_items, fetched_playlist.item_playcount);

            PlaylistHandler.DeletePlaylist(fetched_playlist.name);
            PlaylistHandler.SavePlaylist(edited_playlist);
            main_page.GeneratePlaylist(edited_playlist);
            MessageBox.Show($"Completed edit of {fetched_playlist.name}!", "Completed playlist editing", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }
    }
}
