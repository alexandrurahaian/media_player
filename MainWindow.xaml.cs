using Media_Player.Objects;
using Media_Player.Scripts;
using Media_Player.Windows;
using Microsoft.Win32;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
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
using System.Xml;


namespace Media_Player
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public enum PlayerState
    {
        Paused,
        Playing,
        None     // initial state so the player doesnt increment if the video was paused and unpaused.
    }

    public partial class MainWindow : Window
    {
        public static int media_volume = 15;
        public static int current_file_index = 0;
        public static PlayerState current_state = PlayerState.None;
        private PlaylistPage playlist_page;
        private PlaylistObject? opened_playlist = null;

        public static AppSettings? fetched_settings = null;

        public static bool is_looping = false;
        public static bool is_shuffled = false;
        public static bool handling_media = false;
        public static double current_media_seconds = 1;
        public static bool compact_mode_enabled = false;
        public static bool has_items = false;

        public static CompactWindow ?compact_window = null;

        public Thread ?update_seek_bar_thread = null;

        private List<ListViewItem> previous_order = new List<ListViewItem>();

        public static string format = @"mm\:ss";
        public static string sprite_path = "/Light/";

        public readonly ImmutableList<string> VALID_FILE_EXTENSIONS = new List<string> {"mp3", "mp4", "m4a", }.ToImmutableList<string>();

        private static Stopwatch playtime_watch = new Stopwatch();
        public static Stopwatch session_watch = new Stopwatch();
        public MainWindow()
        {
            InitializeComponent();
            InitialiseMenuItemIcons();
            if (!Directory.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "Backups")))
                Directory.CreateDirectory(System.IO.Path.Combine(AppContext.BaseDirectory, "Backups"));


            video_out_display.LoadedBehavior = MediaState.Manual;
            fetched_settings = AppHandler.InitSettings();
            if (fetched_settings.drp == true) DiscordRichPresenceHandler.InitialiseClient(); else DiscordRichPresenceHandler.Dispose();

            if (fetched_settings.save_files)
            {
                List<string> previouslySaved = SettingsHandler.GetPreviouslySavedFiles();
                if (previouslySaved.Count > 0)
                {
                    LoadFiles(previouslySaved.ToArray(), false);
                }
            }

            if (fetched_settings.resume_on_enter)
            {
                (string fetched_path, double fetched_position) fetched_media_data = SettingsHandler.GetMediaData();
                if (!string.IsNullOrEmpty(fetched_media_data.fetched_path) && System.IO.Path.Exists(fetched_media_data.fetched_path))
                {
                    TimeSpan video_span = video_out_display.Position;
                    PlayMedia(fetched_media_data.fetched_path, true, true, false);
                    video_out_display.Position = TimeSpan.FromSeconds(fetched_media_data.fetched_position);
                    media_position_slider.Value = (video_span.TotalSeconds / fetched_media_data.fetched_position) * 100;
                    Console.WriteLine(media_position_slider.Value);
                    current_pos_display.Text = video_out_display.Position.ToString(format);
                    current_state = PlayerState.None;
                }
            }

            if (fetched_settings.theme == "dark")
            {
                App app = App.Current as App;
                sprite_path = "/Dark/";
                app?.SwitchTheme();
            }

            playlist_page = new PlaylistPage(playlist_contents.Items);
            page_display_frame.Navigate(playlist_page);
            page_display_frame.IsEnabled = false;
            page_display_frame.Visibility = Visibility.Hidden;
            video_out_display.MediaEnded += Video_out_display_MediaEnded;
            this.Closed += MainWindow_Closed;

            if (playlist_contents.Items.Count > 0)
            {
                has_items = true;
            }

            StatisticsObject.Load();
            session_watch.Start();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (fetched_settings == null) return;
            string auto_update_file = System.IO.Path.Combine(AppContext.BaseDirectory, "auto_update.cfg");
            if (File.Exists(auto_update_file))
            {
                bool auto_update = File.ReadAllText(auto_update_file) == "true";
                if (auto_update)
                {
                    await UpdateManager.CheckForUpdates();
                }
            }
            
            if (fetched_settings.announce_updater_ver == true)
            {
                bool? is_latest = await UpdateManager.IsLatestUpdater();
                Debug.WriteLine($"Is latest: {is_latest}");
                if (is_latest == true && MessageBox.Show($"Updater is not on the latest version. Would you like to update it? You can disable this popup from the settings.", "Updater is not up to date.", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                   await UpdateManager.CheckForUpdates();
            }

            if (fetched_settings.delete_old_backups == true)
            {
                await Externals.ClearOldBackups(fetched_settings.backup_lifespan);
            }
        }

        private void InitialiseMenuItemIcons()
        {
            open_file_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri($"/Sprites/file_go.png", UriKind.RelativeOrAbsolute))
            };
            open_folder_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri($"/Sprites/folder_go.png", UriKind.RelativeOrAbsolute))
            };
            clear_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri($"/Sprites/bin_clear.png", UriKind.RelativeOrAbsolute))
            };
            open_playlist_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri($"/Sprites/playlist_go.png", UriKind.RelativeOrAbsolute))
            };
            return_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri($"/Sprites/cross.png", UriKind.RelativeOrAbsolute))
            };
            play_from_playlist_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri($"/Sprites/menu_play.png", UriKind.RelativeOrAbsolute))
            };
            clear_saved_options_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri($"/Sprites/bin_brush.png", UriKind.RelativeOrAbsolute))
            };
            change_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri($"/Sprites/pencil.png", UriKind.RelativeOrAbsolute))
            };
            export_playlist_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri("/Sprites/export.png", UriKind.RelativeOrAbsolute))
            };
            import_playlist_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri("/Sprites/import.png", UriKind.RelativeOrAbsolute))
            };
            export_statistics_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri("/Sprites/export_stats.png", UriKind.RelativeOrAbsolute))
            };
            import_statistics_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri("/Sprites/import_stats.png", UriKind.RelativeOrAbsolute))
            };
            make_backup_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri("/Sprites/create_backup.png", UriKind.RelativeOrAbsolute))
            };
            import_backup_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri("/Sprites/import_backup.png", UriKind.RelativeOrAbsolute))
            };
            view_stats_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri("/Sprites/view_stats.png", UriKind.RelativeOrAbsolute))
            };
            clear_stats_menu_btn.Icon = new Image()
            {
                Source = new BitmapImage(new Uri("/Sprites/clear_stats.png", UriKind.RelativeOrAbsolute))
            };
        }
        public void UpdateVideoPositionBar()
        {

            media_position_slider.Dispatcher.Invoke(new Action(() =>
            {
                media_position_slider.Value = media_position_slider.Minimum;
                current_pos_display.Text = "00:00";
            }));

            if (compact_window != null)
            {
                compact_window.total_display.Dispatcher.Invoke(new Action(() =>
                {
                    compact_window.media_pos_bar.Value = compact_window.media_pos_bar.Minimum;
                    compact_window.current_display.Text = "00:00";
                }));

            }

            while (current_state == PlayerState.Playing)
            {
                if (compact_mode_enabled && compact_window != null)
                {
                    Slider compact_slider = compact_window.media_pos_bar;
                    TextBlock compact_curr_dis = compact_window.current_display;
                    TextBlock compact_total_dis = compact_window.total_display;

                    compact_curr_dis.Dispatcher.Invoke(new Action(() =>
                    {
                        TimeSpan video_span = video_out_display.Position;
                        compact_slider.Value = (video_span.TotalSeconds / current_media_seconds) * 100;

                        compact_curr_dis.Text = video_out_display.Position.ToString(format);
                        if (video_out_display.NaturalDuration.HasTimeSpan)
                        {
                            compact_window.total_display.Text = video_out_display.NaturalDuration.TimeSpan.ToString(format);
                        }

                        if (compact_slider.Value >= compact_slider.Maximum)
                        {
                            compact_slider.Value = compact_slider.Minimum;
                        }
                    }));
                }
                else
                {
                    current_pos_display.Dispatcher.Invoke(new Action(() =>
                    {
                        TimeSpan video_span = video_out_display.Position;
                        media_position_slider.Value = (video_span.TotalSeconds / current_media_seconds) * 100;

                        current_pos_display.Text = video_out_display.Position.ToString(format);

                        if (media_position_slider.Value >= media_position_slider.Maximum)
                        {
                            media_position_slider.Value = media_position_slider.Minimum;
                        }

                    }));
                }

                Thread.Sleep(1000); // each second updates the bar
            }
        }
        private static readonly Random random = new Random();
        public void Shuffle() // shuffles playlist items using the fisher-yates shuffle
        {
            if (!previous_order.Any())
            {
                previous_order.AddRange(playlist_contents.Items.Cast<ListViewItem>());
            }
            playlist_contents.Items.Clear();
            List<ListViewItem> copy = new List<ListViewItem>(previous_order);
            for (int i = 0; i < previous_order.Count; i++)
            {
                int randInt = random.Next(i + 1);
                ListViewItem temp = copy[i];
                copy[i] = copy[randInt];
                copy[randInt] = temp;
            }
            foreach (ListViewItem item in copy)
            {
                ListViewItem new_item = new ListViewItem();
                new_item.Name = item.Name;
                new_item.Content = item.Content;
                new_item.Tag = item.Tag;
                playlist_contents.Items.Add(new_item);
            }
        }

        private void LoadFiles(string[] file_names, bool display_invalid = true)
        {
            List<string> invalid_paths = new List<string>();
            int count = 0;
            playlist_contents.Items.Clear();
            foreach (string file_name in file_names)
            {
                if (System.IO.Path.Exists(file_name) && VALID_FILE_EXTENSIONS.Contains(System.IO.Path.GetExtension(file_name).ToLower().Replace(".", "")))
                {
                    string actual_name = System.IO.Path.GetFileName(file_name); // gets just the file name and extension to display while keeping the actual path
                    ListViewItem playlist_item = new ListViewItem();
                    playlist_item.Name = $"Item{count}";
                    playlist_item.Content = actual_name;
                    playlist_item.Tag = file_name;
                    count++;
                    playlist_contents.Items.Add(playlist_item);
                }
                else if (!System.IO.Path.Exists(file_name) && VALID_FILE_EXTENSIONS.Contains(System.IO.Path.GetExtension(file_name).ToLower().Replace(".", "")))
                {
                    invalid_paths.Add(file_name);
                }
            }
            playlist_items_display.Text = $"Items: {count}";
            if (playlist_page != null)
            {
                playlist_page.opened_files_list.Items.Clear();
                foreach (ListViewItem item in playlist_contents.Items)
                {
                    ListViewItem new_item = new ListViewItem();
                    new_item.Name = item.Name;
                    new_item.Content = item.Content;
                    new_item.Tag = item.Tag;
                    playlist_page.opened_files_list.Items.Add(new_item);
                }
            }

            if (invalid_paths.Count > 0 && display_invalid)
            {
                string outP = string.Empty;
                foreach (string f in  invalid_paths)
                {
                    outP += $"{f}\n";
                }
                MessageBox.Show($"""
                    ! WARNING !

                    Found {invalid_paths.Count} invalid paths:
                    {outP}
                    """, $"{invalid_paths.Count} invalid paths found", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (playlist_contents.Items.Count > 0) has_items = true;
        }

        private int previously_bold_index = 0;
        private void BoldenCurrentlyPlaying()
        {
            foreach (ListViewItem xpp in playlist_contents.Items) // resets all the fonts
            {
                xpp.FontWeight = FontWeights.Normal;
            }

            ListViewItem? current = playlist_contents.Items[current_file_index] as ListViewItem;

            if (previously_bold_index != current_file_index && previously_bold_index < playlist_contents.Items.Count)
            {
                ListViewItem? previous = playlist_contents.Items[previously_bold_index] as ListViewItem;
                if (previous != null) previous.FontWeight = FontWeights.Normal;
            }
            if (current != null) current.FontWeight = FontWeights.Bold;
            previously_bold_index = current_file_index;
        }

        //private void Unbolden()
        //{
        //    if (playlist_contents.Items.Count <= 0) return;
        //    ListViewItem? previous = playlist_contents.Items[previously_bold_index] as ListViewItem;
        //    if (previous != null) previous.FontWeight = FontWeights.Normal;
        //}

        public async Task PlayMedia(string media_file_name, bool overwrite = false, bool increment = true, bool auto_play = true)
        {
            handling_media = true;
            StatisticsObject.TracksPlayed++;
            string extension = System.IO.Path.GetExtension(media_file_name);
            if (extension != ".mp4")
            {
                video_out_display.Visibility = Visibility.Hidden;
                audio_file_image_display.Visibility = Visibility.Visible;
                audio_file_extension_display.Visibility = Visibility.Visible;
                audio_file_extension_display.Text = extension;
            }
            else
            {
                video_out_display.Visibility = Visibility.Visible;
                audio_file_image_display.Visibility = Visibility.Hidden;
                audio_file_extension_display.Visibility = Visibility.Hidden;
            }

            if (auto_play)
            {
                current_state = PlayerState.Playing;
            }
            else
            {
                current_state = PlayerState.Paused;
            }

            string name = System.IO.Path.GetFileName(media_file_name).Replace(System.IO.Path.GetExtension(media_file_name), "");
            if (overwrite || video_out_display.Source == null)
            {
                video_out_display.Source = null;
                video_out_display.Position = TimeSpan.Zero;
                await Task.Delay(150);
                video_out_display.Source = new Uri(media_file_name);
                media_title_display.Text = name;
            }
            video_out_display.Volume = volume_slider.Value / 100;

            if (auto_play)
            {
                video_out_display.Play();
            }

            if (opened_playlist != null)
            {
                playlist_display.Text = $"Playlist: {opened_playlist.name}";
                song_plays_display.Visibility = Visibility.Visible;
                song_plays_display.IsEnabled = true;
                song_plays_display.Text = $"Media plays: {UtilityHandler.GetSongPlays(media_file_name, opened_playlist)}";
            }

            if (increment && opened_playlist != null && auto_play)
            {
                UtilityHandler.IncrementSong(media_file_name, opened_playlist);
                int plays = UtilityHandler.GetSongPlays(media_file_name, opened_playlist);
                song_plays_display.Text = $"Media plays: {plays}";

                // higher than the currently most listened track --> will change that track without needing to call UtilityHandler.GetMostListenedSong()
                if (plays > StatisticsObject.MostListenedTrackPlays)
                {
                    StatisticsObject.MostListenedTrackPlays = plays;
                    StatisticsObject.MostListenedTrack = media_file_name;
                }
            }
            else if (opened_playlist == null)
            {
                song_plays_display.Visibility = Visibility.Hidden;
                playlist_display.Text = "No playlist selected";
            }

            if (update_seek_bar_thread == null || !update_seek_bar_thread.IsAlive && auto_play)
            {
                update_seek_bar_thread = new Thread(UpdateVideoPositionBar);
                update_seek_bar_thread.IsBackground = true;
                update_seek_bar_thread.Start();
            }
            else if (update_seek_bar_thread.ThreadState == System.Threading.ThreadState.Unstarted && auto_play)
            {
                update_seek_bar_thread.Start();
            }

            pause_btn.Dispatcher.Invoke(new Action(() =>
            {
                pause_btn.Content = new Image
                {
                    Source = new BitmapImage(new Uri($"/Sprites{sprite_path}pause.png", UriKind.RelativeOrAbsolute))
                };
            }));
            playtime_watch.Start(); // starts the stopwatch to count the time playing songs
            handling_media = false;
            BoldenCurrentlyPlaying();
        }
        public TimeSpan CalculateNewTimespan()
        {
            double slider_pos = Math.Clamp(media_position_slider.Value, media_position_slider.Minimum, media_position_slider.Maximum);
            double media_duration = video_out_display.NaturalDuration.TimeSpan.TotalSeconds;

            double target_seconds = (slider_pos / media_position_slider.Maximum) * media_duration;

            return TimeSpan.FromSeconds(target_seconds);
        }
        public void Seek()
        {
            if (!compact_mode_enabled)
            {
                current_state = PlayerState.Playing;
                video_out_display.Pause();
                TimeSpan new_timespan = CalculateNewTimespan();
                video_out_display.Position = new_timespan;
                update_seek_bar_thread = new Thread(UpdateVideoPositionBar);
                update_seek_bar_thread.IsBackground = true;
                update_seek_bar_thread.Start();
                video_out_display.Play();
            }
            else
            {
                current_state = PlayerState.Playing;
                video_out_display.Pause();
                TimeSpan new_timespan = compact_window.CalculateNewTimespan();
                video_out_display.Position = new_timespan;
                update_seek_bar_thread = new Thread(UpdateVideoPositionBar);
                update_seek_bar_thread.IsBackground = true;
                update_seek_bar_thread.Start();
                video_out_display.Play();
            }
        }
        private void media_position_slider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            Seek();
        }
        private void media_position_slider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            current_state = PlayerState.Paused; // stops the update thread
        }

        private void video_out_display_MediaOpened(object sender, RoutedEventArgs e)
        {
            double total_minutes = video_out_display.NaturalDuration.TimeSpan.TotalMinutes;
            if (total_minutes >= 60)
            {
                format = @"hh\:mm\:ss";
            }
            else
            {
                format = @"mm\:ss";
            }
            if (compact_mode_enabled && compact_window != null)
            {
                compact_window.total_display.Text = video_out_display.NaturalDuration.TimeSpan.ToString(format);
                compact_window.current_display.Text = string.Format(format, 0);
                current_media_seconds = video_out_display.NaturalDuration.TimeSpan.TotalSeconds;
                if (playlist_contents.Items.Count > 0)
                {
                    compact_window.song_title.Text = System.IO.Path.GetFileNameWithoutExtension(((ListViewItem)playlist_contents.Items[current_file_index]).Tag.ToString());
                    compact_window.current_playlist.Text = playlist_display.Text;
                    if (current_file_index + 1 < playlist_contents.Items.Count)
                    {
                        compact_window.next_song_display.Text = System.IO.Path.GetFileNameWithoutExtension(((ListViewItem)playlist_contents.Items[current_file_index + 1]).Tag.ToString());
                    }
                    else
                    {
                        compact_window.next_song_display.Text = System.IO.Path.GetFileNameWithoutExtension(((ListViewItem)playlist_contents.Items[0]).Tag.ToString());
                    }
                }
            }
            else
            {
                total_pos_display.Text = video_out_display.NaturalDuration.TimeSpan.ToString(format);
                current_pos_display.Text = string.Format(format, 0);
                current_media_seconds = video_out_display.NaturalDuration.TimeSpan.TotalSeconds;
            }
            if (fetched_settings.drp == true) DiscordRichPresenceHandler.UpdatePresence(song_plays_display.Text, media_title_display.Text, video_out_display.NaturalDuration.TimeSpan, (video_out_display.Source.LocalPath.Contains("mp4") ? DiscordRPC.ActivityType.Watching : DiscordRPC.ActivityType.Listening), new DiscordRPC.Assets { LargeImageKey = "idle_img", LargeImageText = media_title_display.Text });
        }

        private async void ActionMedia(bool increment = true)
        {
            if (playlist_contents.Items.Count > 0)
            {
                handling_media = true;
                current_state = PlayerState.Paused;

                ListViewItem? selected = playlist_contents.Items[current_file_index] as ListViewItem;
                if (selected != null)
                {
                    await PlayMedia(selected.Tag.ToString(), overwrite: true, increment: increment);
                    if (compact_mode_enabled && compact_window != null)
                    {
                        compact_window.current_display.Text = string.Format(format, 0);
                        compact_window.song_title.Text = selected.Tag.ToString();
                        compact_window.current_playlist.Text = playlist_display.Text;
                        if (current_file_index + 1 < playlist_contents.Items.Count)
                        {
                            compact_window.next_song_display.Text = System.IO.Path.GetFileName(((ListViewItem)playlist_contents.Items[current_file_index + 1]).Tag.ToString());
                        }
                        else
                        {
                            compact_window.next_song_display.Text = System.IO.Path.GetFileName(((ListViewItem)playlist_contents.Items[0]).Tag.ToString());
                        }
                    }
                }
            }
        }

        private void Video_out_display_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (handling_media) return;

            current_state = PlayerState.Paused;
            
            if (!is_looping)
            {
                current_file_index++;
            }

            if (current_file_index >= playlist_contents.Items.Count)
            {
                current_file_index = 0;
            }
            update_seek_bar_thread = null;
            playtime_watch.Stop();
            ActionMedia();
        }
        public void SwitchPlayColor(string dir)
        {
            sprite_path = dir;
            previous_song_btn.Content = new Image
            {
                Source = new BitmapImage(new Uri($"/Sprites{dir}previous.png", UriKind.RelativeOrAbsolute))
            };
            rewind_current_song_btn.Content = new Image
            {
                Source = new BitmapImage(new Uri($"/Sprites{dir}rewind.png", UriKind.RelativeOrAbsolute))
            };
            shuffle_btn.Content = new Image
            {
                Source = (!is_shuffled) ? new BitmapImage(new Uri($"/Sprites{dir}shuffle_untriggered.png", UriKind.RelativeOrAbsolute)) : new BitmapImage(new Uri($"/Sprites{dir}shuffle_triggered.png", UriKind.RelativeOrAbsolute))
            };
            pause_btn.Content = new Image
            {
                Source = (current_state == PlayerState.Paused) ? new BitmapImage(new Uri($"/Sprites{dir}pause.png", UriKind.RelativeOrAbsolute)) : new BitmapImage(new Uri($"/Sprites{dir}play.png", UriKind.RelativeOrAbsolute))
            };
            repeat_btn.Content = new Image
            {
                Source = (!is_looping) ? new BitmapImage(new Uri($"/Sprites{dir}repeat_untriggered.png", UriKind.RelativeOrAbsolute)) : new BitmapImage(new Uri($"/Sprites{dir}repeat_triggered.png", UriKind.RelativeOrAbsolute))
            };
            skip_song_btn.Content = new Image
            {
                Source = new BitmapImage(new Uri($"/Sprites{dir}skip.png", UriKind.RelativeOrAbsolute))
            };
            mute_unmute_btn.Content = new Image
            {
                Source = ((int)video_out_display.Volume <= 0) ? new BitmapImage(new Uri($"/Sprites{dir}unmute.png", UriKind.RelativeOrAbsolute)) : new BitmapImage(new Uri($"/Sprites{dir}mute.png", UriKind.RelativeOrAbsolute))
            };
            audio_file_image_display.Source = new BitmapImage(new Uri($"/Sprites{dir}audio_file_image.png", UriKind.RelativeOrAbsolute));
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // statistics stuff
            playtime_watch.Stop();
            session_watch.Stop();
            StatisticsObject.TimeListened += playtime_watch.Elapsed.TotalSeconds;
            StatisticsObject.CurrentSessionTime = session_watch.Elapsed.TotalSeconds;
            if (StatisticsObject.CurrentSessionTime > StatisticsObject.HighestSessionTime)
                StatisticsObject.HighestSessionTime = StatisticsObject.CurrentSessionTime;
            StatisticsObject.AverageSessionTime += (StatisticsObject.CurrentSessionTime - StatisticsObject.AverageSessionTime) / (double)++StatisticsObject.Sessions;



            StatisticsObject.Save();
            // settings saving

            if (fetched_settings.save_files && playlist_contents.Items.Count > 0)
            {
                List<string> to_saveList = new List<string>();
                foreach (ListViewItem item in playlist_contents.Items)
                {
                    to_saveList.Add(item.Tag.ToString());
                }
                SettingsHandler.SaveLoadedFiles(to_saveList);
            }

            if (fetched_settings.resume_on_enter && video_out_display.Source != null)
            {
                string current_source = video_out_display.Source.LocalPath;
                double position = video_out_display.Position.TotalSeconds;

                if (!string.IsNullOrEmpty(current_source))
                {
                    SettingsHandler.SaveMediaData(current_source, position);
                }
            }

            if (compact_mode_enabled && compact_window != null)
            {
                compact_window.Close();
            }

            DiscordRichPresenceHandler.Dispose();
            current_state = PlayerState.None;
        }

        public void Rewind()
        {
            current_state = PlayerState.Paused;
            ActionMedia(false);
        }
        private void rewind_current_song_btn_Click(object sender, RoutedEventArgs e)
        {
            Rewind();
        }

        public void Previous()
        {
            if (playlist_contents.Items.Count > 1)
            {
                current_file_index--;
                if (current_file_index < 0)
                {
                    current_file_index = 0;
                }
                ActionMedia();
            } 
        }
        private void previous_song_btn_Click(object sender, RoutedEventArgs e)
        {
            Previous();
        }
        private async void pause_btn_Click(object sender, RoutedEventArgs e)
        {
            if (current_state == PlayerState.Paused || current_state == PlayerState.None)
            {
                if (current_file_index < playlist_contents.Items.Count)
                {
                    current_state = PlayerState.Playing;
                    pause_btn.Content = new Image
                    {
                        Source = new BitmapImage(new Uri($"/Sprites{sprite_path}pause.png", UriKind.RelativeOrAbsolute))
                    };
                    ListViewItem? media_item = playlist_contents.Items[current_file_index] as ListViewItem;

                    if (media_item != null)
                    {
                        bool to_increment = (current_state == PlayerState.None) ? true : false;
                        await PlayMedia(media_item.Tag.ToString(), increment: to_increment);
                    }
                    else
                    {
                        MessageBox.Show($"Something went wrong while attempting to fetch object at {current_file_index}", "Couldn't fetch object at index", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

            }
            else if (current_state == PlayerState.Playing)
            {
                current_state = PlayerState.Paused;
                pause_btn.Content = new Image
                {
                    Source = new BitmapImage(new Uri($"/Sprites{sprite_path}play.png", UriKind.RelativeOrAbsolute))
                };
                video_out_display.Pause();
                playtime_watch.Stop();
            }
        }

        public void Skip()
        {
            if (playlist_contents.Items.Count > 0)
            {
                current_state = PlayerState.Paused;
                current_file_index++;
                if (current_file_index >= playlist_contents.Items.Count)
                {
                    current_file_index = 0;
                }
                ActionMedia();
            }
        }
        private void skip_song_btn_Click(object sender, RoutedEventArgs e)
        {
            Skip();
        }

        private void view_stats_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            StatsWindow sw = new StatsWindow();
            sw.ShowDialog();
        }

        private void clear_stats_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you wish to clear all your stats? This cannot be undone!", "Clearing statistics...", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            
            StatisticsObject.Clear();
        }

        private void open_file_menu_btn_Click(object sender, RoutedEventArgs e)
        {
           OpenFileDialog dialog = new OpenFileDialog();
            dialog.Multiselect = true;
            dialog.InitialDirectory = AppContext.BaseDirectory;
            dialog.Title = "Select files to load";
            previous_order.Clear();
            if (dialog.ShowDialog() == true)
            {
                string[] fileName = dialog.FileNames;
                LoadFiles(fileName);
                foreach (ListViewItem item in playlist_contents.Items)
                {
                    previous_order.Add(item);
                }
            }
        }

        private void open_folder_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                string[] file_names = Directory.GetFiles(dialog.FolderName);
                LoadFiles(file_names);
                previous_order.Clear();
                foreach (ListViewItem item in playlist_contents.Items)
                {
                    previous_order.Add(item);
                }
            }
        }

        private void open_playlist_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            media_controls.Visibility = Visibility.Hidden;
            media_controls.IsEnabled = false;
            page_display_frame.Visibility = Visibility.Visible;
            page_display_frame.IsEnabled = true;
        }

        private void return_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            media_controls.Visibility = Visibility.Visible;
            media_controls.IsEnabled = true;
            page_display_frame.Visibility = Visibility.Hidden;
            page_display_frame.IsEnabled = false;

            if (playlist_page != null)
            {
                playlist_page.opened_files_list.Dispatcher.Invoke(new Action(() =>
                {
                    playlist_page.opened_files_list.Items.Clear();
                }));
            }
        }

        private void play_from_playlist_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            List<PlaylistObject> fetched_playlists = PlaylistHandler.LoadPlaylists();
            if (fetched_playlists.Count <= 0)
            {
                MessageBox.Show("Please create a playlist first!", "No playlists found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            SelectPlaylistWindow new_window = new SelectPlaylistWindow(fetched_playlists);
            if (new_window.ShowDialog() == true)
            {
                PlaylistObject? selected = PlaylistHandler.selected_playlist;
                if (selected != null)
                {
                    previous_order.Clear();
                    is_shuffled = false;
                    shuffle_btn.Content = new Image { Source = new BitmapImage(new Uri($"/Sprites{sprite_path}shuffle_untriggered.png", UriKind.RelativeOrAbsolute)) };

                    LoadFiles(selected.playlist_items.ToArray());
                    opened_playlist = selected;
                }
            }
        }

        private void clear_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            playlist_contents.Items.Clear();
            has_items = false;
            playlist_items_display.Text = "Items: 0";
        }

        public void UpdateVolume()
        {
            volume_display.Text = $"Volume: {((int)volume_slider.Value).ToString()}";
            video_out_display.Volume = volume_slider.Value / 100;
        }
        private void volume_slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateVolume();
        }

        public void ToggleRepeat()
        {
            if (!is_looping)
            {
                repeat_btn.Content = new Image
                {
                    Source = new BitmapImage(new Uri($"/Sprites{sprite_path}repeat_triggered.png", UriKind.RelativeOrAbsolute))
                };
                is_looping = true;
            }
            else
            {
                repeat_btn.Content = new Image
                {
                    Source = new BitmapImage(new Uri($"/Sprites{sprite_path}repeat_untriggered.png", UriKind.RelativeOrAbsolute))
                };
                is_looping = false;
            }
        }
        private void repeat_btn_Click(object sender, RoutedEventArgs e)
        {
            ToggleRepeat();
        }
        public void ShuffleMain()
        {
            if (playlist_contents.Items.Count > 0)
            {
                if (!is_shuffled)
                {
                    is_shuffled = true;
                    string p = null;
                    if (is_looping) p = video_out_display.Source.LocalPath;
                    Shuffle();
                    if (is_looping)
                    {
                        int x = 0;
                        foreach (ListViewItem item in playlist_contents.Items)
                        {
                            if (!string.IsNullOrEmpty(p) && p.Contains(item.Content.ToString()))
                            {
                                current_file_index = x;
                                break;
                            }
                            x++;
                        }
                    }

                    shuffle_btn.Content = new Image
                    {
                        Source = new BitmapImage(new Uri($"/Sprites{sprite_path}shuffle_triggered.png", UriKind.RelativeOrAbsolute))
                    };
                }
                else if (is_shuffled && previous_order.Count > 0)
                {
                    is_shuffled = false;
                    string file_name = media_title_display.Text;
                    int c = 0;
                    foreach (ListViewItem item in previous_order)
                    {
                        if (item.Content.ToString().Contains(file_name))
                        {
                            current_file_index = c;
                            break;
                        }
                        Console.WriteLine(c);
                        c++;
                    }
                    shuffle_btn.Content = new Image
                    {
                        Source = new BitmapImage(new Uri($"/Sprites{sprite_path}shuffle_untriggered.png", UriKind.RelativeOrAbsolute))
                    };
                    playlist_contents.Items.Clear();
                    foreach (ListViewItem prev_item in previous_order)
                    {
                        ListViewItem new_item = new ListViewItem();
                        new_item.Name = prev_item.Name;
                        new_item.Tag = prev_item.Tag;
                        new_item.Content = prev_item.Content;
                        playlist_contents.Items.Add(new_item);
                    }
                }
            }
            else
            {
                MessageBox.Show("Please add items before attempting to shuffle!", "No items to shuffle", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void shuffle_btn_Click(object sender, RoutedEventArgs e)
        {
            ShuffleMain();

            foreach (ListViewItem item in playlist_contents.Items)
            {
                if (item.Content.ToString().Contains(media_title_display.Text))
                {
                    item.FontWeight = FontWeights.Bold;
                    break;
                }
            }
        }

        private async void playlist_contents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListViewItem? selected_item = playlist_contents.SelectedItem as ListViewItem;
            if (selected_item != null)
            {
                current_file_index = playlist_contents.SelectedIndex;
                current_state = PlayerState.Paused;
                await PlayMedia(selected_item.Tag.ToString(), true, true);
                pause_btn.Content = new Image { Source = new BitmapImage(new Uri($"/Sprites{sprite_path}pause.png", UriKind.RelativeOrAbsolute)) };
            }
        }
        private void opened_searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {

            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(playlist_contents.Items);

            if (!string.IsNullOrEmpty(opened_searchbox.Text))
            {
                view.Filter = item =>
                {
                    var listItem = item as ListViewItem;
                    string v = listItem?.Content.ToString() ?? string.Empty;
                    return v.Contains(opened_searchbox.Text, StringComparison.OrdinalIgnoreCase);
                };
            }
            else view.Filter = null;

            // old implementation

            //if (string.IsNullOrEmpty(opened_searchbox.Text))
            //{
            //    Unbolden();
            //    previously_bold_index = 0;
            //    RestoreItemsBeforeSearch();
            //    return;
            //}

            //List<ListViewItem> results = new List<ListViewItem>();

            //foreach (ListViewItem item in playlist_contents.Items)
            //{
            //    if (item.Tag.ToString().ToLower().Contains(opened_searchbox.Text.ToLower()))
            //    {
            //        results.Add(item);
            //    }
            //}

            //if (results.Count > 0)
            //{
            //    playlist_contents.Items.Clear();
            //    foreach (ListViewItem result in  results)
            //    {
            //        playlist_contents.Items.Add(result);
            //    }
            //}
            //else if (results.Count <= 0)
            //{
            //    RestoreItemsBeforeSearch();
            //}
        }

        private void info_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"""
                Media Player {UpdateManager.GetCurrentAppVersion()}

                Getting started:
                To start using the media player you can either open a folder or some files to add to the list.
                You can also create a playlist.

                The difference between adding regular files or folders and loading a playlist is that media files in playlists will have their play counts increased.

                Supported file formats: Most file formats supported by the default Windows Media Player (mp3, mp4, m4a, etc.)

                Options panel:

                The options panel allows you to toggle various features such as file list saving which will load all the items in the list on next startup.
                This won't load the playlist if they were apart of one!
                You can also toggle resume on enter which will automatically resume the last media that was being played by the player at the exact
                position. (Will not automatically play it, just load it)

                Compact mode:
                
                Compact mode allows you to get rid of the gigantic media player interface with useless features for general listening.
                You can also toggle always on top for compact mode which will display it over any other processes.
                """);
        }

        public static bool createdOptions = false;
        private void options_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            if (!createdOptions)
            {
                createdOptions = true;
                OptionsWindow newOptions = new OptionsWindow(this);
                newOptions.Show();
            }
        }

        private void clear_saved_options_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you wish to clear all options data? (saved files, resume on play)\nThis action cannot be reversed!", "Clear options data", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                SettingsHandler.Clear();
                fetched_settings.save_files = false;
                fetched_settings.resume_on_enter = false;
                if (fetched_settings.theme != "light")
                {
                    App app = App.Current as App;
                    app.SwitchTheme();
                }
                fetched_settings.theme = "light";
            }
        }

        private void mute_unmute_btn_Click(object sender, RoutedEventArgs e)
        {
            if (volume_slider.Value > 0)
            {
                media_volume = (int)volume_slider.Value;
                volume_slider.Value = 0;
                UpdateVolume();
                mute_unmute_btn.Content = new Image()
                {
                    Source = new BitmapImage(new Uri($"/Sprites{sprite_path}mute.png", UriKind.RelativeOrAbsolute))
                };
            }
            else if (volume_slider.Value < 1)
            {
                volume_slider.Value = media_volume;
                UpdateVolume();
                mute_unmute_btn.Content = new Image()
                {
                    Source = new BitmapImage(new Uri($"/Sprites{sprite_path}unmute.png", UriKind.RelativeOrAbsolute))
                };
            }
        }

        private async void export_playlist_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistHandler.GetPlaylists().Count == 0)
            {
                MessageBox.Show("No playlists to export!", "Error: no playlists found!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ExportWindow ew = new ExportWindow();
            ew.ShowDialog();
        }
        private async void export_statistics_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select place to export statistics file";
            ofd.Multiselect = false;
            if (ofd.ShowDialog() == true)
                await Externals.ExportStatistics(ofd.FolderName);
        }

        private async void import_playlist_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select files to import";
            ofd.Multiselect = true;
            ofd.Filter = $"Media Player Playlist File|*{Externals.PLAYLIST_EXPORT_FILE_EXTENSION}";

            if (ofd.ShowDialog() == true)
            {
                if (fetched_settings.make_backups)
                {
                    bool backupResult = await Externals.Backup();
                    if (!backupResult)
                    {
                        MessageBox.Show($"Backup failed, cancelled importing!", "Backup failed!", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                
                await Externals.Import(ofd.FileNames);
            }
        }

        private async void import_statistics_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select files to import";
            ofd.Multiselect = false;
            ofd.Filter = $"Media Player Statistics File|*{Externals.STATISTICS_EXPORT_FILE_EXTENSION}";

            if (ofd.ShowDialog() == true)
            {
                if (fetched_settings.make_backups)
                {
                    bool backupResult = await Externals.Backup();
                    if (!backupResult)
                    {
                        MessageBox.Show($"Backup failed, cancelled importing!", "Backup failed!", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                
                await Externals.ImportStatistics(ofd.FileName);
            }
        }

        private async void make_backup_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "Backups"))) return;

            bool result = await Externals.Backup();
            if (result) MessageBox.Show($"Created backup in {System.IO.Path.Combine(AppContext.BaseDirectory, "Backups")}", "Successfully created backup!", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            else MessageBox.Show($"Could not create backup!", "Failed created backup!", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async void import_backup_menu_btn_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "Backups"))) return;
            if (MessageBox.Show("! WARNING !\nIF YOU IMPORT A BACKUP FILE, YOUR CURRENT DATABASE CONTENTS (STATISTICS AND PLAYLISTS) WILL BE WIPED AND THE DATA REPLACED WITH THE ONE IN THE BACKUP FILE! THIS CANNOT BE REVERTED! ARE YOU SURE YOU WISH TO CONTINUE?", "! WARNING !", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = System.IO.Path.Combine(AppContext.BaseDirectory, "Backups");
            ofd.Multiselect = false;
            ofd.Filter = $"Media Player Backup File|*{Externals.BACKUP_FILE_EXTENSION}";
            
            if (ofd.ShowDialog() == true)
            {
                bool result = await Externals.ImportBackup(ofd.FileName);
                if (!result) MessageBox.Show($"Failed to import backup, cancelled.", "Backup import failed!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void video_out_display_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MessageBox.Show($"Media failed because of: {e.ErrorException}");
        }
    }
}