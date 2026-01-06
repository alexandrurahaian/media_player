using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Media_Player.Objects
{
    public class AppSettings
    {
        public bool resume_on_enter { get; set; } = false;
        public bool save_files { get; set; } = false;
        public string theme { get; set; } = "light";
        public bool drp { get; set; } = true;
        public bool delete_old_backups { get; set; } = false;
        public bool make_backups { get; set; } = true;
        public bool announce_updater_ver { get; set; } = true;
        public int backup_lifespan { get; set; } = 60; // in days
    }
}
