using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader.Config;

namespace StatLimiters {
	public class StatLimiterConfig : ModConfig {
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static StatLimiterConfig Instance;
		public bool ShowHealthLimiter = false;
		public bool ShowManaLimiter = false;
		[DefaultValue(true)]
		public bool ShowJumpLimiter = true;
		[DefaultValue(true)]
		public bool ShowSpeedLimiter = true;
	}
}
