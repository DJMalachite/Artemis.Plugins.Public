﻿using Artemis.Core.DataModelExpansions;
using System.Drawing;
using WindowsDisplayAPI;
using WindowsDisplayAPI.Native.DeviceContext;
using WindowsDisplayAPI.Native.DisplayConfig;

namespace Artemis.Plugins.DataModelExpansions.DisplaySettings.DataModels
{
    public class DisplaySettingsDataModel : DataModel
    {
        public DisplayConfigTopologyId Topology { get; set; }
        public int DisplayCount { get; set; }
        public DisplaysDataModel Displays { get; } = new();
    }

    public class DisplaysDataModel : DataModel { }

    public class DisplaySettingDataModel : DataModel
    {
        public string Name { get; set; }
        public int Number { get; set; }
        public string Adapter { get; set; }
        public bool IsPrimary { get; set; }
        [DataModelProperty(Affix = "Hz")]
        public int RefreshRate { get; set; }
        public Size Resolution { get; set; }
        public Point Position { get; set; }
        public ColorDepth ColorDepth { get; set; }
        public DisplayOrientation DisplayRotation { get; set; }
    }
}