using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Unigram.Services.Settings
{
    public class WallpaperSettings : SettingsServiceBase
    {
        public WallpaperSettings(ApplicationDataContainer container)
            : base(container)
        {

        }

        private long? _selectedBackground;
        public long SelectedBackground
        {
            get
            {
                if (_selectedBackground == null)
                    _selectedBackground = GetValueOrDefault("SelectedBackgroundL", 1000001L);

                return _selectedBackground ?? 1000001L;
            }
            set
            {
                _selectedBackground = value;
                AddOrUpdateValue("SelectedBackgroundL", value);
            }
        }

        private int? _selectedColor;
        public int SelectedColor
        {
            get
            {
                if (_selectedColor == null)
                    _selectedColor = GetValueOrDefault("SelectedColor", 0);

                return _selectedColor ?? 0;
            }
            set
            {
                _selectedColor = value;
                AddOrUpdateValue("SelectedColor", value);
            }
        }



        private bool? _isBlurEnabled;
        public bool IsBlurEnabled
        {
            get
            {
                if (_isBlurEnabled == null)
                    _isBlurEnabled = GetValueOrDefault("IsBlurEnabled", false);

                return _isBlurEnabled ?? false;
            }
            set
            {
                _isBlurEnabled = value;
                AddOrUpdateValue("IsBlurEnabled", value);
            }
        }

        private bool? _isMotionEnabled;
        public bool IsMotionEnabled
        {
            get
            {
                if (_isMotionEnabled == null)
                    _isMotionEnabled = GetValueOrDefault("IsMotionEnabled", false);

                return _isMotionEnabled ?? false;
            }
            set
            {
                _isMotionEnabled = value;
                AddOrUpdateValue("IsMotionEnabled", value);
            }
        }
    }
}
