using Media_Player.Objects;
using Media_Player.Scripts;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Dynamic;
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
using System.Windows.Shapes;

namespace Media_Player
{
    /// <summary>
    /// Interaction logic for OptionsWindow.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        private AppSettings settings = MainWindow.fetched_settings;
        private MainWindow actual = null;
        public OptionsWindow(MainWindow window)
        {
            InitializeComponent();

            compact_mode_checkbox.IsEnabled = MainWindow.has_items;

            if (MainWindow.compact_mode_enabled)
            {
                compact_mode_checkbox.IsChecked = true;
                compact_aot_panel.Visibility = Visibility.Visible;
            }
            else
            {
                compact_aot_panel.Visibility = Visibility.Collapsed;
            }

            if (File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "auto_update.cfg")))
            {
                string val = File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "auto_update.cfg"));
                auto_update_check.IsChecked = val.ToLower().Contains("true");
            }
            else auto_update_check.IsChecked = false;

            resume_checkbox.IsChecked = settings.resume_on_enter;
            save_opened_files_checkbox.IsChecked = settings.save_files;
            drp_checkbox.IsChecked = settings.drp;
            dark_mode_check.IsChecked = settings.theme == "dark";
            this.Closing += OptionsWindow_Closing;
            backup_lifespan_box.TextChanged += Backup_lifespan_box_TextChanged;
            actual = window;
        }

        private void Backup_lifespan_box_TextChanged(object sender, TextChangedEventArgs e)
        {
            int val;
            if (int.TryParse(backup_lifespan_box.Text, out val))
                settings.backup_lifespan = val;
            else backup_lifespan_box.Text = "7";
        }

        private void OptionsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            AppHandler.SaveFile(settings);
            MainWindow.createdOptions = false;
        }

        private void compact_mode_checkbox_Click(object sender, RoutedEventArgs e)
        {
            if (compact_mode_checkbox.IsChecked == true && MainWindow.has_items)
            {
                MainWindow.compact_window = new CompactWindow(actual);
                MainWindow.compact_window.Show();
                MainWindow.compact_window.song_title.Text = actual.media_title_display.Text;
                MainWindow.compact_window.media_pos_bar.Value = actual.media_position_slider.Value;
                MainWindow.compact_window.current_display.Text = actual.current_pos_display.Text;
                MainWindow.compact_window.total_display.Text = actual.total_pos_display.Text;
                MainWindow.compact_window.volume_bar.Value = actual.volume_slider.Value;
                MainWindow.compact_window.volume_display.Text = actual.volume_display.Text;

                int current_index = MainWindow.current_file_index;
                if (current_index + 1 < actual.playlist_contents.Items.Count)
                {
                    ListViewItem ?item = actual.playlist_contents.Items[current_index + 1] as ListViewItem;
                    if (item != null)
                    {
                        MainWindow.compact_window.next_song_display.Text = $"Next song: {System.IO.Path.GetFileNameWithoutExtension(item.Tag.ToString())}";
                    }
                }
                else
                {
                    ListViewItem ?item = actual.playlist_contents.Items[0] as ListViewItem;
                    if (item != null)
                    {
                        MainWindow.compact_window.next_song_display.Text = $"Next song: {System.IO.Path.GetFileNameWithoutExtension(item.Tag.ToString())}";
                    }
                }

                MainWindow.compact_mode_enabled = true;
                this.Close();
            }
            else
            {
                if (MainWindow.compact_window != null)
                {
                    MainWindow.compact_window.Close();
                }
                MainWindow.compact_mode_enabled = false;
                compact_aot_panel.Visibility = Visibility.Collapsed;
            }
        }

        private void resume_checkbox_Click(object sender, RoutedEventArgs e)
        {
            settings.resume_on_enter = (bool)resume_checkbox.IsChecked;
        }

        private void save_opened_files_checkbox_Click(object sender, RoutedEventArgs e)
        {
            settings.save_files = (bool)save_opened_files_checkbox.IsChecked;
        }

        private void compact_always_on_top_check_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.compact_window != null)
            {
                MainWindow.compact_window.Topmost = (bool)compact_always_on_top_check.IsChecked;
            }
        }

        private void drp_checkbox_Click(object sender, RoutedEventArgs e)
        {
            settings.drp = (bool)drp_checkbox.IsChecked;
            if (settings.drp == true) DiscordRichPresenceHandler.InitialiseClient();
            else DiscordRichPresenceHandler.Dispose();
        }

        private void dark_mode_check_Click(object sender, RoutedEventArgs e)
        {
            string prev = settings.theme;
            if (dark_mode_check.IsChecked == true)
                settings.theme = "dark";
            else
                settings.theme = "light";

            if (settings.theme != prev)
            {
                App app = App.Current as App;
                app.SwitchTheme();
            }
        }

        private void auto_create_backups_check_Click(object sender, RoutedEventArgs e)
        {
            settings.make_backups = auto_create_backups_check.IsChecked == true;
        }

        private void auto_remove_backups_Click(object sender, RoutedEventArgs e)
        {
            settings.delete_old_backups = auto_remove_backups.IsChecked == true;
            lifespan_panel.Visibility = (auto_remove_backups.IsChecked == true ? Visibility.Visible : Visibility.Hidden);
        }

        private void auto_update_check_Click(object sender, RoutedEventArgs e)
        {
            string auto_update_file = System.IO.Path.Combine(AppContext.BaseDirectory, "auto_update.cfg");
            try
            {
                if (File.Exists(auto_update_file))
                {
                    bool auto_update = (File.ReadAllText(auto_update_file) == "true" ? true : false);

                    if (auto_update && auto_update_check.IsChecked == false)
                        File.WriteAllText(auto_update_file, "false");
                    else if (!auto_update && auto_update_check.IsChecked == true)
                        File.WriteAllText(auto_update_file, "true");
                }
                else
                {
                    string to_write = (auto_update_check.IsChecked == true).ToString().ToLower();
                    File.WriteAllText(auto_update_file, to_write);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not change auto update file: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}