using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using rail;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using static Terraria.ModLoader.BuilderToggle;

namespace StatLimiters {
	public record class MaxStatSource(Func<Player, int> GetCount, int StatPerUse);
	public class HealthLimiter : StatLimiterPlayer {
		public static List<MaxStatSource> MaxLifeSources { get; } = [
			new MaxStatSource(player => player.ConsumedLifeCrystals, 20),
			new MaxStatSource(player => player.ConsumedLifeFruit, 5),
		];
		public override int Ticks => MaxLifeSources.Sum(s => s.GetCount(Player));
		public override LocalizedText ToggleDisplayValue => base.ToggleDisplayValue.WithFormatArgs(ReductionText(GetReduction()));
		public override bool Active() => StatLimiterConfig.Instance.ShowHealthLimiter && MaxLifeSources.Any(s => s.GetCount(Player) > 0);
		public int GetReduction() {
			int reduction = 0;
			int remainingLess = currentLimit;
			for (int i = MaxLifeSources.Count; i-- > 0;) {
				int used = MaxLifeSources[i].GetCount(Player);
				int lessness = Math.Min(remainingLess, used);
				reduction += lessness * MaxLifeSources[i].StatPerUse;
				remainingLess -= lessness;
			}
			return reduction;
		}
		public override void ModifyMaxStats(out StatModifier health, out StatModifier mana) {
			base.ModifyMaxStats(out health, out mana);
			health.Base -= GetReduction();
		}
	}
	public class ManaLimiter : StatLimiterPlayer {
		public static List<MaxStatSource> MaxManaSources { get; } = [
			new MaxStatSource(player => player.ConsumedManaCrystals, 20)
		];
		public override int Ticks => MaxManaSources.Sum(s => s.GetCount(Player));
		public override LocalizedText ToggleDisplayValue => base.ToggleDisplayValue.WithFormatArgs(ReductionText(GetReduction()));
		public override bool Active() => StatLimiterConfig.Instance.ShowManaLimiter && MaxManaSources.Any(s => s.GetCount(Player) > 0);
		public override Position OrderPosition => new After(ModContent.GetInstance<HealthLimiter>().BuilderToggle);
		public int GetReduction() {
			int reduction = 0;
			int remainingLess = currentLimit;
			for (int i = MaxManaSources.Count; i-- > 0;) {
				int used = MaxManaSources[i].GetCount(Player);
				int lessness = Math.Min(remainingLess, used);
				reduction += lessness * MaxManaSources[i].StatPerUse;
				remainingLess -= lessness;
			}
			return reduction;
		}
		public override void ModifyMaxStats(out StatModifier health, out StatModifier mana) {
			base.ModifyMaxStats(out health, out mana);
			mana.Base -= GetReduction();
		}
	}
	public class JumpLimiter : StatLimiterPlayer {
		public override int Ticks => 17;
		public int TotalAmount => Ticks + 3;
		public override LocalizedText ToggleDisplayValue => base.ToggleDisplayValue.WithFormatArgs(ReductionText(currentLimit / (float)TotalAmount));
		public override bool Active() => StatLimiterConfig.Instance.ShowJumpLimiter;
		public override void PostUpdateRunSpeeds() {
			float factor = MathF.Pow(currentLimit / (float)TotalAmount, 2);
			Player.jumpHeight -= (int)(Player.jumpHeight * factor);
			Player.jumpSpeed *= 1 - factor;
		}
	}
	public class SpeedLimiter : StatLimiterPlayer {
		public override int Ticks => 19;
		public int TotalAmount => Ticks + 1;
		public override LocalizedText ToggleDisplayValue => base.ToggleDisplayValue.WithFormatArgs(ReductionText(currentLimit / (float)TotalAmount));
		public override bool Active() => StatLimiterConfig.Instance.ShowSpeedLimiter;
		public override void PostUpdateRunSpeeds() {
			float factor = 1 - currentLimit / (float)TotalAmount;
			Player.maxRunSpeed *= factor;
			Player.accRunSpeed *= factor;
		}
	}
	public abstract class StatLimiterPlayer : ModPlayer {
		public int currentLimit = 0;
		public abstract int Ticks { get; }
		public abstract bool Active();
		public virtual LocalizedText ToggleDisplayValue => Language.GetOrRegister(Mod.GetLocalizationKey($"StatLimiters.{Name}"));
		public virtual Position OrderPosition => new Default();
		public StatLimiterBuilderToggle BuilderToggle { get; private set; }
		public override void Load() {
			Mod.AddContent(BuilderToggle = new StatLimiterBuilderToggle(this));
			OnLoad();
		}
		public virtual void OnLoad() { }
		public override void SetStaticDefaults() {
			_ = Language.GetOrRegister(Mod.GetLocalizationKey($"StatLimiters.{Name}"));
		}
		public static string ReductionText(int reduction) => reduction == 0 ? Language.GetOrRegister($"Mods.StatLimiters.StatLimiters.NotLimited").Value : (-reduction).ToString();
		public static string ReductionText(float reductionPercent) => reductionPercent == 0 ? Language.GetOrRegister($"Mods.StatLimiters.StatLimiters.NotLimited").Value : $"-{reductionPercent:P0}";
	}
	[Autoload(false)]
	public class StatLimiterBuilderToggle(StatLimiterPlayer player) : BuilderToggle {
		public StatLimiterPlayer LocalPlayer => Main.LocalPlayer.GetModPlayer(player);
		public override Position OrderPosition => player.OrderPosition;
		public override bool Active() => LocalPlayer.Active();
		public override string Name => $"{base.Name}_{player.Name}";
		public override string DisplayValue() => LocalPlayer.ToggleDisplayValue.Value;
		public override string HoverTexture => Texture;
		public override bool OnLeftClick(ref SoundStyle? sound) {
			StatLimiterSystem.InterfaceLayer.SetActive(player);
			return false;
		}
		public override bool Draw(SpriteBatch spriteBatch, ref BuilderToggleDrawParams drawParams) {
			drawParams.Frame.Y = 0;
			drawParams.Frame.Height = 18;
			if (LocalPlayer.currentLimit == 0) drawParams.Color = drawParams.Color.MultiplyRGB(Color.Gray);
			return true;
		}
		public override bool DrawHover(SpriteBatch spriteBatch, ref BuilderToggleDrawParams drawParams) {
			drawParams.Frame.Y = 20;
			drawParams.Frame.Height = 20;
			return true;
		}
		public override void OnRightClick() {
			LocalPlayer.currentLimit = 0;
		}
	}
	public class StatLimiters : Mod { }
	public class StatLimiterSystem : ModSystem {
		public static LimiterInterfaceLayer InterfaceLayer { get; private set; } = new();
		public override void Unload() => InterfaceLayer = null;
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Builder Accessories Bar");
			if (index != -1) {
				layers.Insert(index + 1, InterfaceLayer);
			}
		}
	}
	public class LimiterInterfaceLayer() : GameInterfaceLayer($"{nameof(StatLimiters): Limiter UI}", InterfaceScaleType.UI) {
		public Asset<Texture2D> ColorBar = ModContent.Request<Texture2D>($"{nameof(StatLimiters)}/{nameof(ColorBar)}");
		public Asset<Texture2D> ColorSlider = ModContent.Request<Texture2D>($"{nameof(StatLimiters)}/{nameof(ColorSlider)}");
		public int offsetY = 0;
		public StatLimiterPlayer player;
		public void SetActive(StatLimiterPlayer basePlayer) {
			Active = true;
			player = Main.LocalPlayer.GetModPlayer(basePlayer);
			offsetY = Math.Max(Main.mouseY - (int)((player.currentLimit / (float)player.Ticks) * 178), 8);
		}
		protected override bool DrawSelf() {
			if (!Main.mouseLeft || player is null) {
				Active = false;
				return true;
			}
			Vector2 position = new(10, offsetY);
			Main.spriteBatch.Draw(
				ColorBar.Value,
				position,
				null,
				Color.White,
				MathHelper.PiOver2,
				new(2, 7),
				1f,
				0,
			0);
			int numTicks = player.Ticks;
			float nearestTickDistance = float.PositiveInfinity;
			if (numTicks > 1) {
				float tickIncrement = 1f / numTicks;
				for (int tick = 0; tick <= numTicks; tick++) {
					float percent = tick * tickIncrement;
					int yPosition = (int)(position.Y + percent * 178);
					float dist = Math.Abs(yPosition - Main.mouseY);
					if (dist < nearestTickDistance) {
						nearestTickDistance = dist;
						player.currentLimit = tick;
					}
					if (percent > 0 && percent < 1f) {
						Main.spriteBatch.Draw(
							TextureAssets.MagicPixel.Value,
							new Rectangle(
								(int)(position.X - 7),
								yPosition - 1,
								14,
								2
							),
							Color.White
						);
					}
				}
			}
			Main.spriteBatch.Draw(
				ColorSlider.Value,
				position + new Vector2(0, (player.currentLimit / (float)numTicks) * 178 + 1),
				null,
				Color.White,
				MathHelper.PiOver2,
				new(5, 8),
				1f,
				0,
			0);
			Main.mouseText = true;
			Main.instance.MouseText(player.ToggleDisplayValue.Value);
			return true;
		}
	}
}
