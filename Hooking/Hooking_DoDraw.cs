using BaseLibrary.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.GameContent.UI;
using Terraria.GameInput;
using Terraria.Graphics;
using Terraria.Graphics.Capture;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace RefinedCode.Hooking
{
	public static partial class Hooking
	{
		public static class MenuModeID
		{
			public const int VanillaMenu = 888;
		}

		public static ulong DrawCycleCounter;

		public static ulong TileFrameSeed
		{
			get => typeof(Main).GetValue<ulong>("_tileFrameSeed");
			set => typeof(Main).SetValue("_tileFrameSeed", value);
		}

		public static void InitTargets()
		{
			Main.instance.UpdateDisplaySettings();

			InitTargets(Main.instance.GraphicsDevice.PresentationParameters.BackBufferWidth, Main.instance.GraphicsDevice.PresentationParameters.BackBufferHeight);
		}

		public static int renderTargetMaxSize = typeof(Main).GetValue<int>("_renderTargetMaxSize");

		public static void InitTargets(int width, int height)
		{
			ReleaseTargets();
			Main.offScreenRange = 192;
			if (width + Main.offScreenRange * 2 > renderTargetMaxSize) Main.offScreenRange = (renderTargetMaxSize - width) / 2;

			width += Main.offScreenRange * 2;
			height += Main.offScreenRange * 2;
			try
			{
				if (!Main.dedServ)
				{
					GraphicsDevice device = Main.instance.GraphicsDevice;
					SurfaceFormat format = device.PresentationParameters.BackBufferFormat;

					Main.targetSet = true;
					Main.waterTarget = new RenderTarget2D(device, width, height, false, format, DepthFormat.Depth24);
					Main.instance.backWaterTarget = new RenderTarget2D(device, width, height, false, format, DepthFormat.Depth24);
					Main.instance.blackTarget = new RenderTarget2D(device, width, height, false, format, DepthFormat.Depth24);
					Main.instance.tileTarget = new RenderTarget2D(device, width, height, false, format, DepthFormat.Depth24);
					Main.instance.tile2Target = new RenderTarget2D(device, width, height, false, format, DepthFormat.Depth24);
					Main.instance.wallTarget = new RenderTarget2D(device, width, height, false, format, DepthFormat.Depth24);
					Main.instance.backgroundTarget = new RenderTarget2D(device, width, height, false, format, DepthFormat.Depth24);
					Main.screenTarget = new RenderTarget2D(device, width, height, false, format, DepthFormat.Depth24);
					Main.screenTargetSwap = new RenderTarget2D(device, width, height, false, format, DepthFormat.Depth24);
					uiTarget = new RenderTarget2D(device, width, height, false, format, DepthFormat.Depth24);

					OnRenderTargetsInitialized?.Invoke(width, height);
				}
			}
			catch
			{
				Lighting.lightMode = 2;
				Main.mapEnabled = false;
				Main.SaveSettings();
				try
				{
					ReleaseTargets();
				}
				catch
				{
				}
			}
		}

		public static void ReleaseTargets()
		{
			try
			{
				if (!Main.dedServ)
				{
					Main.offScreenRange = 0;
					Main.targetSet = false;
					if (Main.waterTarget != null) Main.waterTarget.Dispose();
					if (Main.instance.backWaterTarget != null) Main.instance.backWaterTarget.Dispose();
					if (Main.instance.blackTarget != null) Main.instance.blackTarget.Dispose();
					if (Main.instance.tileTarget != null) Main.instance.tileTarget.Dispose();
					if (Main.instance.tile2Target != null) Main.instance.tile2Target.Dispose();
					if (Main.instance.wallTarget != null) Main.instance.wallTarget.Dispose();
					if (Main.screenTarget != null) Main.screenTarget.Dispose();
					if (Main.screenTargetSwap != null) Main.screenTargetSwap.Dispose();
					if (Main.instance.backgroundTarget != null) Main.instance.backgroundTarget.Dispose();
					uiTarget?.Dispose();

					OnRenderTargetsReleased?.Invoke();
				}
			}
			catch
			{
			}
		}

		public static MethodInfo miLookForColorTiles = typeof(Main).GetMethod("lookForColorTiles", Utility.defaultFlags);
		public static Action<object> LookForColorTiles = obj => miLookForColorTiles.Invoke(obj, null);
		public static MethodInfo miDrawToMap = typeof(Main).GetMethod("DrawToMap", Utility.defaultFlags);
		public static Action<object> DrawToMap = obj => miDrawToMap.Invoke(obj, null);
		public static Action<GameTime> OnPreDraw = typeof(Main).GetValue<Action<GameTime>>("OnPreDraw");
		public static Action OnRenderTargetsReleased = typeof(Main).GetValue<Action>("OnRenderTargetsReleased");
		public static ResolutionChangeEvent OnRenderTargetsInitialized = typeof(Main).GetValue<ResolutionChangeEvent>("OnRenderTargetsInitialized");

		public static void DoDraw(On.Terraria.Main.orig_do_Draw origDoDraw, Main self, GameTime gameTime)
		{
			Main.superFast = true;

			if (DrawCycleCounter == 0uL) TileFrameSeed = Utils.RandomNextSeed(TileFrameSeed);

			DrawCycleCounter = (DrawCycleCounter + 1uL) % 5uL;

			Main.MenuUI.IsVisible = Main.gameMenu && Main.menuMode == MenuModeID.VanillaMenu;
			Main.InGameUI.IsVisible = !Main.gameMenu && Main.InGameUI.CurrentState != null;
			PlayerInput.UpdateMainMouse();
			CaptureManager.Instance.DrawTick();
			TimeLogger.NewDrawFrame();

			if (!Main.gameMenu) LookForColorTiles(self);

			TimeLogger.DetailedDrawTime(0);
			if (Main.loadMap)
			{
				Main.refreshMap = false;
				DrawToMap(self);
				TimeLogger.DetailedDrawTime(1);
			}

			Main.drawToScreen = Lighting.UpdateEveryFrame;

			if (Main.drawToScreen && Main.targetSet) ReleaseTargets();

			if (!Main.drawToScreen && !Main.targetSet) InitTargets();

			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			Main.fpsCount++;
			if (!self.IsActive) Main.maxQ = true;

#if CLIENT
			this.UpdateDisplaySettings();
#endif

			OnPreDraw(gameTime);

			Main.drawsCountedForFPS++;
			Main.screenLastPosition = Main.screenPosition;

			HandleStackSplit();

			Player player = Main.LocalPlayer;

			if (Main.myPlayer >= 0)
			{
				player.lastMouseInterface = player.mouseInterface;
				player.mouseInterface = false;
			}

			if (Main.mapTime > 0) Main.mapTime--;

			if (Main.gameMenu) Main.mapTime = Main.mapTimeMax;

			Main.HoverItem = new Item();

			HandleCamera();

			typeof(Main).InvokeMethod<object>("CheckMonoliths");

			if (Main.showSplash)
			{
				self.InvokeMethod<object>("DrawSplash", gameTime);
				TimeLogger.SplashDrawTime(stopwatch.Elapsed.TotalMilliseconds);
				TimeLogger.EndDrawFrame();
				return;
			}

			Main.sunCircle += 0.01f;
			if (Main.sunCircle > 6.285) Main.sunCircle -= 6.285f;

			RenderToTargets(gameTime);

			HandleDrawToMap();

			int bgWidth = Main.backgroundWidth[Main.background];
			bgParallax = 0.1;
			bgStart = (int)(-Math.IEEERemainder(Main.screenPosition.X * bgParallax, bgWidth) - bgWidth * 0.5f);
			bgLoops = Main.screenWidth / bgWidth + 2;
			bgStartY = 0;
			bgLoopsY = 0;
			bgTop = (int)(-Main.screenPosition.Y / (Main.worldSurface * 16.0 - 600.0) * 200.0);
			Main.bgColor = Color.White;
			if (Main.gameMenu || Main.netMode == NetmodeID.Server) bgTop = -200;

			#region colors
			int sunPosX = (int)(Main.time / 54000.0 * (Main.screenWidth + Main.sunTexture.Width * 2)) - Main.sunTexture.Width;
			int sunPosY = 0;
			Color sunColor = Color.White;
			float sunScale = 1f;
			float sunRotation = (float)(Main.time / 54000.0) * 2f - 7.3f;
			int moonPosX = (int)(Main.time / 32400.0 * (Main.screenWidth + Main.moonTexture[Main.moonType].Width * 2)) - Main.moonTexture[Main.moonType].Width;
			int moonPosY = 0;
			Color moonColor = Color.White;
			float moonScale = 1f;
			float moonRotation = (float)(Main.time / 32400.0) * 2f - 7.3f;
			if (Main.dayTime)
			{
				double num26;
				if (Main.time < 27000.0)
				{
					num26 = Math.Pow(1.0 - Main.time / 54000.0 * 2.0, 2.0);
					sunPosY = (int)(bgTop + num26 * 250.0 + 180.0);
				}
				else
				{
					num26 = Math.Pow((Main.time / 54000.0 - 0.5) * 2.0, 2.0);
					sunPosY = (int)(bgTop + num26 * 250.0 + 180.0);
				}

				sunScale = (float)(1.2 - num26 * 0.4);
			}
			else
			{
				double num27;
				if (Main.time < 16200.0)
				{
					num27 = Math.Pow(1.0 - Main.time / 32400.0 * 2.0, 2.0);
					moonPosY = (int)(bgTop + num27 * 250.0 + 180.0);
				}
				else
				{
					num27 = Math.Pow((Main.time / 32400.0 - 0.5) * 2.0, 2.0);
					moonPosY = (int)(bgTop + num27 * 250.0 + 180.0);
				}

				moonScale = (float)(1.2 - num27 * 0.4);
			}

			HandleBGColor(ref sunColor, ref moonColor);

			float cloudAlphaStep = 0.0005f * Main.dayRate;
			if (Main.gameMenu) cloudAlphaStep *= 20f;

			if (Main.raining)
			{
				if (Main.cloudAlpha > Main.maxRaining)
				{
					Main.cloudAlpha -= cloudAlphaStep;
					if (Main.cloudAlpha < Main.maxRaining)
					{
						Main.cloudAlpha = Main.maxRaining;
					}
				}
				else if (Main.cloudAlpha < Main.maxRaining)
				{
					Main.cloudAlpha += cloudAlphaStep;
					if (Main.cloudAlpha > Main.maxRaining)
					{
						Main.cloudAlpha = Main.maxRaining;
					}
				}
			}
			else
			{
				Main.cloudAlpha -= cloudAlphaStep;
				if (Main.cloudAlpha < 0f)
				{
					Main.cloudAlpha = 0f;
				}
			}

			if (Main.cloudAlpha > 0f)
			{
				float num30 = 1f - Main.cloudAlpha * 0.9f;
				Main.bgColor.R = (byte)(Main.bgColor.R * num30);
				Main.bgColor.G = (byte)(Main.bgColor.G * num30);
				Main.bgColor.B = (byte)(Main.bgColor.B * num30);
			}

			if (Main.gameMenu || Main.netMode == 2)
			{
				bgTop = 0;
				if (!Main.dayTime)
				{
					Main.bgColor.R = 35;
					Main.bgColor.G = 35;
					Main.bgColor.B = 35;
				}
			}

			if (Main.gameMenu)
			{
				Main.bgDelay = 1000;
				Main.evilTiles = (int)(Main.bgAlpha[1] * 500f);
			}

			if (Main.evilTiles > 0)
			{
				float evilTilesScaled = Main.evilTiles / 500f;
				if (evilTilesScaled > 1f) evilTilesScaled = 1f;

				int bgColorR = Main.bgColor.R;
				int bgColorG = Main.bgColor.G;
				int bgColorB = Main.bgColor.B;
				bgColorR -= (int)(100f * evilTilesScaled * (Main.bgColor.R / 255f));
				bgColorG -= (int)(140f * evilTilesScaled * (Main.bgColor.G / 255f));
				bgColorB -= (int)(80f * evilTilesScaled * (Main.bgColor.B / 255f));

				if (bgColorR < 15) bgColorR = 15;
				if (bgColorG < 15) bgColorG = 15;
				if (bgColorB < 15) bgColorB = 15;

				Main.bgColor.R = (byte)bgColorR;
				Main.bgColor.G = (byte)bgColorG;
				Main.bgColor.B = (byte)bgColorB;
				bgColorR = sunColor.R;
				bgColorG = sunColor.G;
				bgColorB = sunColor.B;
				bgColorR -= (int)(100f * evilTilesScaled * (sunColor.R / 255f));
				bgColorG -= (int)(100f * evilTilesScaled * (sunColor.G / 255f));
				bgColorB -= (int)(0f * evilTilesScaled * (sunColor.B / 255f));

				if (bgColorR < 15) bgColorR = 15;
				if (bgColorG < 15) bgColorG = 15;
				if (bgColorB < 15) bgColorB = 15;

				sunColor.R = (byte)bgColorR;
				sunColor.G = (byte)bgColorG;
				sunColor.B = (byte)bgColorB;
				bgColorR = moonColor.R;
				bgColorG = moonColor.G;
				bgColorB = moonColor.B;
				bgColorR -= (int)(140f * evilTilesScaled * (moonColor.R / 255f));
				bgColorG -= (int)(190f * evilTilesScaled * (moonColor.G / 255f));
				bgColorB -= (int)(170f * evilTilesScaled * (moonColor.B / 255f));

				if (bgColorR < 15) bgColorR = 15;
				if (bgColorG < 15) bgColorG = 15;
				if (bgColorB < 15) bgColorB = 15;

				moonColor.R = (byte)bgColorR;
				moonColor.G = (byte)bgColorG;
				moonColor.B = (byte)bgColorB;
			}

			if (Main.bloodTiles > 0)
			{
				float bloodTilesScaled = Main.bloodTiles / 400f;
				if (bloodTilesScaled > 1f) bloodTilesScaled = 1f;

				int bgColorR = Main.bgColor.R;
				int bgColorG = Main.bgColor.G;
				int bgColorB = Main.bgColor.B;
				bgColorR -= (int)(70f * bloodTilesScaled * (Main.bgColor.G / 255f));
				bgColorG -= (int)(110f * bloodTilesScaled * (Main.bgColor.G / 255f));
				bgColorB -= (int)(150f * bloodTilesScaled * (Main.bgColor.B / 255f));

				if (bgColorR < 15) bgColorR = 15;
				if (bgColorG < 15) bgColorG = 15;
				if (bgColorB < 15) bgColorB = 15;

				Main.bgColor.R = (byte)bgColorR;
				Main.bgColor.G = (byte)bgColorG;
				Main.bgColor.B = (byte)bgColorB;
				bgColorR = sunColor.R;
				bgColorG = sunColor.G;
				bgColorB = sunColor.B;
				bgColorG -= (int)(90f * bloodTilesScaled * (sunColor.G / 255f));
				bgColorB -= (int)(110f * bloodTilesScaled * (sunColor.B / 255f));

				if (bgColorR < 15) bgColorR = 15;
				if (bgColorG < 15) bgColorG = 15;
				if (bgColorB < 15) bgColorB = 15;

				sunColor.R = (byte)bgColorR;
				sunColor.G = (byte)bgColorG;
				sunColor.B = (byte)bgColorB;
				bgColorR = moonColor.R;
				bgColorG = moonColor.G;
				bgColorB = moonColor.B;
				bgColorR -= (int)(100f * bloodTilesScaled * (moonColor.R / 255f));
				bgColorG -= (int)(120f * bloodTilesScaled * (moonColor.G / 255f));
				bgColorB -= (int)(180f * bloodTilesScaled * (moonColor.B / 255f));

				if (bgColorR < 15) bgColorR = 15;
				if (bgColorG < 15) bgColorG = 15;
				if (bgColorB < 15) bgColorB = 15;

				moonColor.R = (byte)bgColorR;
				moonColor.G = (byte)bgColorG;
				moonColor.B = (byte)bgColorB;
			}

			if (Main.jungleTiles > 0)
			{
				float jungleTilesScaled = Main.jungleTiles / 200f;
				if (jungleTilesScaled > 1f) jungleTilesScaled = 1f;

				int bgColorR = Main.bgColor.R;
				int bgColorG = Main.bgColor.G;
				int bgColorB = Main.bgColor.B;
				bgColorR -= (int)(40f * jungleTilesScaled * (Main.bgColor.R / 255f));
				bgColorB -= (int)(70f * jungleTilesScaled * (Main.bgColor.B / 255f));

				if (bgColorR > 15) bgColorR = 255;
				if (bgColorG > 15) bgColorG = 255;
				if (bgColorB > 15) bgColorB = 255;
				if (bgColorR < 15) bgColorR = 15;
				if (bgColorG < 15) bgColorG = 15;
				if (bgColorB < 15) bgColorB = 15;

				Main.bgColor.R = (byte)bgColorR;
				Main.bgColor.G = (byte)bgColorG;
				Main.bgColor.B = (byte)bgColorB;
				bgColorR = sunColor.R;
				bgColorG = sunColor.G;
				bgColorB = sunColor.B;
				bgColorR -= (int)(30f * jungleTilesScaled * (sunColor.R / 255f));
				bgColorB -= (int)(10f * jungleTilesScaled * (sunColor.B / 255f));

				if (bgColorR < 15) bgColorR = 15;
				if (bgColorG < 15) bgColorG = 15;
				if (bgColorB < 15) bgColorB = 15;

				sunColor.R = (byte)bgColorR;
				sunColor.G = (byte)bgColorG;
				sunColor.B = (byte)bgColorB;
				bgColorR = moonColor.R;
				bgColorG = moonColor.G;
				bgColorB = moonColor.B;
				bgColorG -= (int)(140f * jungleTilesScaled * (moonColor.R / 255f));
				bgColorR -= (int)(170f * jungleTilesScaled * (moonColor.G / 255f));
				bgColorB -= (int)(190f * jungleTilesScaled * (moonColor.B / 255f));

				if (bgColorR < 15) bgColorR = 15;
				if (bgColorG < 15) bgColorG = 15;
				if (bgColorB < 15) bgColorB = 15;

				moonColor.R = (byte)bgColorR;
				moonColor.G = (byte)bgColorG;
				moonColor.B = (byte)bgColorB;
			}

			if (Main.shroomTiles > 0)
			{
				float shroomTilesScaled = Main.shroomTiles / 160f;
				if (shroomTilesScaled > Main.shroomLight) Main.shroomLight += 0.01f;
				else if (shroomTilesScaled < Main.shroomLight) Main.shroomLight -= 0.01f;
			}
			else Main.shroomLight -= 0.02f;

			if (Main.shroomLight < 0f) Main.shroomLight = 0f;
			if (Main.shroomLight > 1f) Main.shroomLight = 1f;

			if (Main.shroomLight > 0f)
			{
				float shroomLight = Main.shroomLight;
				int bgColorR = Main.bgColor.R;
				int bgColorG = Main.bgColor.G;
				int bgColorB = Main.bgColor.B;
				bgColorG -= (int)(250f * shroomLight * (Main.bgColor.G / 255f));
				bgColorR -= (int)(250f * shroomLight * (Main.bgColor.R / 255f));
				bgColorB -= (int)(250f * shroomLight * (Main.bgColor.B / 255f));

				if (bgColorR < 15) bgColorR = 15;
				if (bgColorG < 15) bgColorG = 15;
				if (bgColorB < 15) bgColorB = 15;

				Main.bgColor.R = (byte)bgColorR;
				Main.bgColor.G = (byte)bgColorG;
				Main.bgColor.B = (byte)bgColorB;
				bgColorR = sunColor.R;
				bgColorG = sunColor.G;
				bgColorB = sunColor.B;
				bgColorG -= (int)(10f * shroomLight * (sunColor.G / 255f));
				bgColorR -= (int)(30f * shroomLight * (sunColor.R / 255f));
				bgColorB -= (int)(10f * shroomLight * (sunColor.B / 255f));

				if (bgColorR < 15) bgColorR = 15;
				if (bgColorG < 15) bgColorG = 15;
				if (bgColorB < 15) bgColorB = 15;

				sunColor.R = (byte)bgColorR;
				sunColor.G = (byte)bgColorG;
				sunColor.B = (byte)bgColorB;
				bgColorR = moonColor.R;
				bgColorG = moonColor.G;
				bgColorB = moonColor.B;
				bgColorG -= (int)(140f * shroomLight * (moonColor.R / 255f));
				bgColorR -= (int)(170f * shroomLight * (moonColor.G / 255f));
				bgColorB -= (int)(190f * shroomLight * (moonColor.B / 255f));

				if (bgColorR < 15) bgColorR = 15;
				if (bgColorG < 15) bgColorG = 15;
				if (bgColorB < 15) bgColorB = 15;

				moonColor.R = (byte)bgColorR;
				moonColor.G = (byte)bgColorG;
				moonColor.B = (byte)bgColorB;
			}

			if (Lighting.NotRetro)
			{
				if (Main.bgColor.R < 10) Main.bgColor.R = 10;
				if (Main.bgColor.G < 10) Main.bgColor.G = 10;
				if (Main.bgColor.B < 10) Main.bgColor.B = 10;
			}
			else
			{
				if (Main.bgColor.R < 15) Main.bgColor.R = 15;
				if (Main.bgColor.G < 15) Main.bgColor.G = 15;
				if (Main.bgColor.B < 15) Main.bgColor.B = 15;
			}

			if (Main.bloodMoon)
			{
				if (Main.bgColor.R < 25) Main.bgColor.R = 25;
				if (Main.bgColor.G < 25) Main.bgColor.G = 25;
				if (Main.bgColor.B < 25) Main.bgColor.B = 25;
			}

			if (Main.eclipse && Main.dayTime)
			{
				Main.eclipseLight = (float)(Main.time / 1242f);
				if (Main.eclipseLight > 1f) Main.eclipseLight = 1f;
			}
			else if (Main.eclipseLight > 0f)
			{
				Main.eclipseLight -= 0.01f;
				if (Main.eclipseLight < 0f) Main.eclipseLight = 0f;
			}

			if (Main.eclipseLight > 0f)
			{
				float num49 = 1f - 0.925f * Main.eclipseLight;
				float num50 = 1f - 0.96f * Main.eclipseLight;
				float num51 = 1f - 1f * Main.eclipseLight;
				int num52 = (int)(Main.bgColor.R * num49);
				int num53 = (int)(Main.bgColor.G * num50);
				int num54 = (int)(Main.bgColor.B * num51);
				Main.bgColor.R = (byte)num52;
				Main.bgColor.G = (byte)num53;
				Main.bgColor.B = (byte)num54;
				sunColor.R = 255;
				sunColor.G = 127;
				sunColor.B = 67;
				if (Main.bgColor.R < 20) Main.bgColor.R = 20;
				if (Main.bgColor.G < 10) Main.bgColor.G = 10;

				if (!Lighting.NotRetro)
				{
					if (Main.bgColor.R < 20) Main.bgColor.R = 20;
					if (Main.bgColor.G < 14) Main.bgColor.G = 14;
					if (Main.bgColor.B < 6) Main.bgColor.B = 6;
				}
			}

			Main.tileColor.A = 255;
			int sum = Main.bgColor.R + Main.bgColor.G + Main.bgColor.B;
			Main.tileColor.R = (byte)((sum + Main.bgColor.R * 7) * 0.1f);
			Main.tileColor.G = (byte)((sum + Main.bgColor.G * 7) * 0.1f);
			Main.tileColor.B = (byte)((sum + Main.bgColor.B * 7) * 0.1f);
			Main.tileColor = SkyManager.Instance.ProcessTileColor(Main.tileColor);

			object[] param = { Main.tileColor, Main.bgColor };
			ModHooks.InvokeMethod<object>("ModifySunLight", param);
			Main.tileColor = (Color)param[0];
			Main.bgColor = (Color)param[1];

			float num55 = Main.maxTilesX / 4200f;
			num55 *= num55;
			atmo = (float)(((Main.screenPosition.Y + Main.screenHeight * 0.5f) / 16f - (65f + 10f * num55)) / (Main.worldSurface / 5.0)).Clamp(0f, 1f);

			if (Main.gameMenu) atmo = 1f;

			Main.bgColor.R = (byte)(Main.bgColor.R * atmo);
			Main.bgColor.G = (byte)(Main.bgColor.G * atmo);
			Main.bgColor.B = (byte)(Main.bgColor.B * atmo);
			if (atmo <= 0.05)
			{
				Main.bgColor.R = 0;
				Main.bgColor.G = 0;
				Main.bgColor.B = 0;
				Main.bgColor.A = 0;
			}
			#endregion

			self.GraphicsDevice.Clear(Color.Black);
			self.InvokeMethod<object>("Draw", gameTime);
			bool removeForcedMinimumZoom = typeof(ModLoader).GetValue<bool>("removeForcedMinimumZoom");
			float val = Main.screenWidth / (removeForcedMinimumZoom ? 1920f : 8192f);
			float val2 = Main.screenHeight / (removeForcedMinimumZoom ? 1200f : 8192f);
			Main.GameViewMatrix.Effects = Main.gameMenu || player.gravDir == 1f ? SpriteEffects.None : SpriteEffects.FlipVertically;
			Main.BackgroundViewMatrix.Effects = Main.GameViewMatrix.Effects;
			Main.ForcedMinimumZoom = Utility.Max(1f, val, val2);
			Main.BackgroundViewMatrix.Zoom = new Vector2(Main.ForcedMinimumZoom);
			Main.GameViewMatrix.Zoom = new Vector2(Main.ForcedMinimumZoom * MathHelper.Clamp(Main.GameZoomTarget, 1f, 2f));
			self.Rasterizer = Main.gameMenu || player.gravDir == 1f ? RasterizerState.CullCounterClockwise : RasterizerState.CullClockwise;

			param = new object[] { Main.GameViewMatrix };
			ModHooks.InvokeMethod<object>("ModifyTransformMatrix", param);
			Main.GameViewMatrix = (SpriteViewMatrix)param[0];

			bool flag = !Main.drawToScreen && Main.netMode != NetmodeID.Server && !Main.gameMenu && !Main.mapFullscreen && Lighting.NotRetro && Filters.Scene.CanCapture();
			if (flag) Filters.Scene.BeginCapture();

			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, self.Rasterizer, null, Main.BackgroundViewMatrix.TransformationMatrix);
			TimeLogger.DetailedDrawReset();
			if (!Main.mapFullscreen)
			{
				self.unityMouseOver = false;
				if (Main.screenPosition.Y < Main.worldSurface * 16.0 + 16.0)
				{
					for (int i = 0; i < bgLoops; i++)
					{
						Main.spriteBatch.Draw(Main.backgroundTexture[Main.background], new Rectangle(bgStart + Main.backgroundWidth[Main.background] * i, bgTop, Main.backgroundWidth[Main.background], Math.Max(Main.screenHeight, Main.backgroundHeight[Main.background])), Main.bgColor);
					}

					TimeLogger.DetailedDrawTime(6);
				}

				Main.spriteBatch.End();
				Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, self.Rasterizer, null, Main.BackgroundViewMatrix.EffectMatrix);
				DrawStars();

				HandleSunAndMoon(player, sunPosX, sunPosY, sunColor, sunScale, sunRotation, moonPosX, moonPosY, moonColor, moonScale, moonRotation);

				TimeLogger.DetailedDrawTime(7);
			}

			Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.Sky);
			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, self.Rasterizer, null, Main.BackgroundViewMatrix.TransformationMatrix);
			self.InvokeMethod<object>("DrawBG");
			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, self.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
			self.InvokeMethod<object>("DrawBackgroundBlackFill");
			Main.spriteBatch.End();
			Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.Landscape, true);
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, self.Rasterizer, null, Main.UIScaleMatrix);
			if (Main.gameMenu || Main.netMode == NetmodeID.Server)
			{
				bool isActive = self.IsActive;
				Rectangle[] rainSource = new Rectangle[6];
				for (int k = 0; k < rainSource.Length; k++)
				{
					rainSource[k] = new Rectangle(k * 4, 0, 2, 40);
				}

				Color color8 = Main.bgColor * 0.85f;
				for (int l = 0; l < Main.maxRain; l++)
				{
					if (Main.rain[l].active)
					{
						Rain rain = Main.rain[l];
						Main.spriteBatch.Draw(Main.rainTexture, rain.position - Main.screenPosition, rainSource[rain.type], color8, rain.rotation, Vector2.Zero, rain.scale, SpriteEffects.None, 0f);
						if (isActive) rain.Update();
					}
				}

				self.InvokeMethod<object>("DrawMenu", gameTime);
				TimeLogger.MenuDrawTime(stopwatch.Elapsed.TotalMilliseconds);
				TimeLogger.EndDrawFrame();
				return;
			}

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, self.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
			firstTileX = (int)Math.Floor(Main.screenPosition.X / 16f) - 1;
			lastTileX = (int)Math.Floor((Main.screenPosition.X + Main.screenWidth) / 16f) + 2;
			firstTileY = (int)Math.Floor(Main.screenPosition.Y / 16f) - 1;
			lastTileY = (int)Math.Floor((Main.screenPosition.Y + Main.screenHeight) / 16f) + 2;
			if (!Main.drawSkip)
			{
				Lighting.LightTiles(firstTileX, lastTileX, firstTileY, lastTileY);
			}

			void DrawFPS() => self.InvokeMethod<object>("DrawFPS");
			void DrawPlayerChat() => self.InvokeMethod<object>("DrawPlayerChat");
			void DrawInterface(params object[] args) => self.InvokeMethod<object>("DrawInterface", args);

			TimeLogger.DetailedDrawReset();
			if (!Main.mapFullscreen)
			{
				void CacheNPCDraws() => self.InvokeMethod<object>("CacheNPCDraws");
				void CacheProjDraws() => self.InvokeMethod<object>("CacheProjDraws");
				void DrawCachedNPCs(params object[] args) => self.InvokeMethod<object>("DrawCachedNPCs", args);
				void DrawCachedProjs(params object[] args) => self.InvokeMethod<object>("DrawCachedProjs", args);
				void DrawBlack(params object[] args) => self.InvokeMethod<object>("DrawBlack", args);
				void DrawWalls() => self.InvokeMethod<object>("DrawWalls");
				void DrawWoF() => self.InvokeMethod<object>("DrawWoF");
				void DrawGoreBehind() => self.InvokeMethod<object>("DrawGoreBehind");
				void DrawTiles(params object[] args) => self.InvokeMethod<object>("DrawTiles", args);
				void DrawNPCs(params object[] args) => self.InvokeMethod<object>("DrawNPCs", args);
				void drawWaters(params object[] args) => self.InvokeMethod<object>("drawWaters", args);
				void SortDrawCacheWorms() => self.InvokeMethod<object>("SortDrawCacheWorms");
				void DrawPlayers() => self.InvokeMethod<object>("DrawPlayers");
				void DrawProjectiles() => self.InvokeMethod<object>("DrawProjectiles");
				void DrawRain() => self.InvokeMethod<object>("DrawRain");
				void DrawItems() => self.InvokeMethod<object>("DrawItems");
				void DrawGore() => self.InvokeMethod<object>("DrawGore");
				void DrawDust() => self.InvokeMethod<object>("DrawDust");
				void DrawWires() => self.InvokeMethod<object>("DrawWires");
				void DrawInfernoRings() => self.InvokeMethod<object>("DrawInfernoRings");

				Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.InWorldUI);
				if (Main.drawToScreen)
				{
					drawWaters(true, -1, true);
				}
				else
				{
					Main.spriteBatch.Draw(self.backWaterTarget, Main.sceneBackgroundPos - Main.screenPosition, Color.White);
					TimeLogger.DetailedDrawTime(11);
				}

				Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.BackgroundWater);
				float x = (Main.sceneBackgroundPos.X - Main.screenPosition.X + Main.offScreenRange) * Main.caveParallax - Main.offScreenRange;
				if (Main.drawToScreen)
				{
					Main.tileBatch.Begin();
					self.InvokeMethod<object>("DrawBackground");
					Main.tileBatch.End();
				}
				else
				{
					Main.spriteBatch.Draw(self.backgroundTarget, new Vector2(x, Main.sceneBackgroundPos.Y - Main.screenPosition.Y), Color.White);
					TimeLogger.DetailedDrawTime(12);
				}

				Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.Background);
				Sandstorm.DrawGrains(Main.spriteBatch);
				ScreenDarkness.DrawBack(Main.spriteBatch);
				Main.magmaBGFrameCounter++;
				if (Main.magmaBGFrameCounter >= 8)
				{
					Main.magmaBGFrameCounter = 0;
					Main.magmaBGFrame++;
					if (Main.magmaBGFrame >= 3) Main.magmaBGFrame = 0;
				}

				try
				{
					CacheNPCDraws();
					CacheProjDraws();
					DrawCachedNPCs(self.DrawCacheNPCsMoonMoon, true);
					if (Main.drawToScreen)
					{
						DrawBlack(false);
						Main.tileBatch.Begin();
						DrawWalls();
						Main.tileBatch.End();
					}
					else
					{
						Main.spriteBatch.Draw(self.blackTarget, Main.sceneTilePos - Main.screenPosition, Color.White);
						TimeLogger.DetailedDrawTime(13);
						Main.spriteBatch.Draw(self.wallTarget, Main.sceneWallPos - Main.screenPosition, Color.White);
						TimeLogger.DetailedDrawTime(14);
					}

					Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.Walls);
					DrawWoF();
					if (Main.drawBackGore)
					{
						Main.drawBackGore = false;
						if (Main.ignoreErrors)
						{
							try
							{
								DrawGoreBehind();
								goto IL_3DDC;
							}
							catch (Exception e)
							{
								TimeLogger.DrawException(e);
								goto IL_3DDC;
							}
						}

						DrawGoreBehind();
					}

				IL_3DDC:
					MoonlordDeathDrama.DrawPieces(Main.spriteBatch);
					MoonlordDeathDrama.DrawExplosions(Main.spriteBatch);
					DrawCachedNPCs(self.DrawCacheNPCsBehindNonSolidTiles, true);
					if (player.detectCreature)
					{
						if (Main.drawToScreen)
						{
							DrawTiles(false, -1);
							TimeLogger.DetailedDrawReset();
							self.waterfallManager.Draw(Main.spriteBatch);
							TimeLogger.DetailedDrawTime(16);
							DrawTiles(true, -1);
						}
						else
						{
							Main.spriteBatch.Draw(self.tile2Target, Main.sceneTile2Pos - Main.screenPosition, Color.White);
							TimeLogger.DetailedDrawTime(15);
							self.waterfallManager.Draw(Main.spriteBatch);
							TimeLogger.DetailedDrawTime(16);
							Main.spriteBatch.Draw(self.tileTarget, Main.sceneTilePos - Main.screenPosition, Color.White);
							TimeLogger.DetailedDrawTime(17);
						}

						TimeLogger.DetailedDrawReset();
						Main.spriteBatch.End();
						DrawCachedProjs(self.DrawCacheProjsBehindNPCsAndTiles, true);
						Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, self.Rasterizer, null, Main.Transform);
						DrawNPCs(true);
						TimeLogger.DetailedDrawTime(18);
						Main.spriteBatch.End();
						DrawCachedProjs(self.DrawCacheProjsBehindNPCs, true);
						Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, self.Rasterizer, null, Main.Transform);
						player.hitTile.DrawFreshAnimations(Main.spriteBatch);
						DrawNPCs(false);
						DrawCachedNPCs(self.DrawCacheNPCProjectiles, false);
						TimeLogger.DetailedDrawTime(19);
					}
					else
					{
						if (Main.drawToScreen)
						{
							DrawCachedNPCs(self.DrawCacheNPCsBehindNonSolidTiles, true);
							DrawTiles(false, -1);
							TimeLogger.DetailedDrawReset();
							self.waterfallManager.Draw(Main.spriteBatch);
							TimeLogger.DetailedDrawTime(16);
							Main.spriteBatch.End();
							DrawCachedProjs(self.DrawCacheProjsBehindNPCsAndTiles, true);
							Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, self.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
							DrawNPCs(true);
							TimeLogger.DetailedDrawTime(18);
							DrawTiles(true, -1);
						}
						else
						{
							DrawCachedNPCs(self.DrawCacheNPCsBehindNonSolidTiles, true);
							Main.spriteBatch.Draw(self.tile2Target, Main.sceneTile2Pos - Main.screenPosition, Color.White);
							TimeLogger.DetailedDrawTime(15);
							self.waterfallManager.Draw(Main.spriteBatch);
							TimeLogger.DetailedDrawTime(16);
							Main.spriteBatch.End();
							DrawCachedProjs(self.DrawCacheProjsBehindNPCsAndTiles, true);
							Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, self.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
							DrawNPCs(true);
							TimeLogger.DetailedDrawTime(18);
							Main.spriteBatch.Draw(self.tileTarget, Main.sceneTilePos - Main.screenPosition, Color.White);
							TimeLogger.DetailedDrawTime(17);
						}

						player.hitTile.DrawFreshAnimations(Main.spriteBatch);
						Main.spriteBatch.End();
						DrawCachedProjs(self.DrawCacheProjsBehindNPCs, true);
						Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, self.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
						TimeLogger.DetailedDrawReset();
						DrawNPCs(false);
						DrawCachedNPCs(self.DrawCacheNPCProjectiles, false);
						TimeLogger.DetailedDrawTime(19);
					}
				}
				catch (Exception e2)
				{
					TimeLogger.DrawException(e2);
				}

				Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.TilesAndNPCs);
				if (!Main.mapFullscreen && Main.mapStyle == 2)
				{
					if (Main.ignoreErrors)
					{
						try
						{
							self.InvokeMethod<object>("DrawMap");

							goto IL_4196;
						}
						catch (Exception e3)
						{
							TimeLogger.DrawException(e3);
							goto IL_4196;
						}
					}

					self.InvokeMethod<object>("DrawMap");
				}

			IL_4196:
				TimeLogger.DetailedDrawReset();
				Main.spriteBatch.End();
				WorldHooks.PostDrawTiles();
				TimeLogger.DetailedDrawTime(35);
				SortDrawCacheWorms();
				DrawCachedProjs(self.DrawCacheProjsBehindProjectiles, true);
				DrawProjectiles();
				DrawPlayers();
				Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, self.Rasterizer, null, Main.Transform);
				DrawCachedNPCs(self.DrawCacheNPCsOverPlayers, false);
				if (!Main.gamePaused)
				{
					Main.essScale += Main.essDir * 0.01f;
					if (Main.essScale > 1f)
					{
						Main.essDir = -1;
						Main.essScale = 1f;
					}

					if (Main.essScale < 0.7)
					{
						Main.essDir = 1;
						Main.essScale = 0.7f;
					}
				}

				DrawItems();
				TimeLogger.DetailedDrawTime(22);
				DrawRain();
				if (Main.ignoreErrors)
				{
					try
					{
						DrawGore();
						goto IL_428C;
					}
					catch (Exception e4)
					{
						TimeLogger.DrawException(e4);
						goto IL_428C;
					}
				}

				DrawGore();
			IL_428C:
				Main.spriteBatch.End();
				DrawDust();
				Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, self.Rasterizer, null, Main.Transform);
				Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.Entities);
				if (Main.drawToScreen)
				{
					drawWaters(false, -1, true);
					if (WiresUI.Settings.DrawWires)
					{
						DrawWires();
					}
				}
				else
				{
					Main.spriteBatch.Draw(Main.waterTarget, Main.sceneWaterPos - Main.screenPosition, Color.White);
					if (WiresUI.Settings.DrawWires)
					{
						DrawWires();
					}

					TimeLogger.DetailedDrawTime(26);
				}

				Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.ForegroundWater);
				DrawCachedProjs(self.DrawCacheProjsOverWiresUI, false);
				DrawInfernoRings();
				ScreenDarkness.DrawFront(Main.spriteBatch);
				MoonlordDeathDrama.DrawWhite(Main.spriteBatch);
				ScreenObstruction.Draw(Main.spriteBatch);
				TimeLogger.DetailedDrawReset();
				Main.spriteBatch.End();
				Overlays.Scene.Draw(Main.spriteBatch, RenderLayers.All, true);
				if (flag)
				{
					Filters.Scene.EndCapture();
				}

				TimeLogger.DetailedDrawTime(36);
				if (!Main.hideUI)
				{
					Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, null, null, null, Main.GameViewMatrix.ZoomMatrix);
					TimeLogger.DetailedDrawReset();
					for (int m = 0; m < Main.player.Length; m++)
					{
						if (Main.player[m].active && Main.player[m].chatOverhead.timeLeft > 0 && !Main.player[m].dead)
						{
							Vector2 messageSize = Main.player[m].chatOverhead.messageSize;
							Vector2 vector5;
							vector5.X = Main.player[m].position.X + Main.player[m].width * 0.5f - messageSize.X / 2f;
							vector5.Y = Main.player[m].position.Y - messageSize.Y - 2f;
							vector5.Y += Main.player[m].gfxOffY;
							vector5 = vector5.Floor();
							if (player.gravDir == -1f)
							{
								vector5.Y -= Main.screenPosition.Y;
								vector5.Y = Main.screenPosition.Y + Main.screenHeight - vector5.Y;
							}

							int num66 = 0;
							ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, Main.fontMouseText, Main.player[m].chatOverhead.snippets, vector5 - Main.screenPosition, 0f, Vector2.Zero, Vector2.One, out num66, -1f, 2f);
						}
					}

					float num67 = CombatText.TargetScale;
					for (int n = 0; n < Main.combatText.Length; n++)
					{
						if (Main.combatText[n].active)
						{
							int num68 = 0;
							if (Main.combatText[n].crit)
							{
								num68 = 1;
							}

							Vector2 vector6 = Main.fontCombatText[num68].MeasureString(Main.combatText[n].text);
							Vector2 origin = new Vector2(vector6.X * 0.5f, vector6.Y * 0.5f);
							float num69 = Main.combatText[n].scale / num67;
							float num70 = Main.combatText[n].color.R;
							float num71 = Main.combatText[n].color.G;
							float num72 = Main.combatText[n].color.B;
							float num73 = Main.combatText[n].color.A;
							num70 *= num69 * Main.combatText[n].alpha * 0.3f;
							num72 *= num69 * Main.combatText[n].alpha * 0.3f;
							num71 *= num69 * Main.combatText[n].alpha * 0.3f;
							num73 *= num69 * Main.combatText[n].alpha;
							Color color9 = new Color((int)num70, (int)num71, (int)num72, (int)num73);
							for (int num74 = 0; num74 < 5; num74++)
							{
								float num75 = 0f;
								float num76 = 0f;
								if (num74 == 0)
								{
									num75 -= num67;
								}
								else if (num74 == 1)
								{
									num75 += num67;
								}
								else if (num74 == 2)
								{
									num76 -= num67;
								}
								else if (num74 == 3)
								{
									num76 += num67;
								}
								else
								{
									num70 = Main.combatText[n].color.R * num69 * Main.combatText[n].alpha;
									num72 = Main.combatText[n].color.B * num69 * Main.combatText[n].alpha;
									num71 = Main.combatText[n].color.G * num69 * Main.combatText[n].alpha;
									num73 = Main.combatText[n].color.A * num69 * Main.combatText[n].alpha;
									color9 = new Color((int)num70, (int)num71, (int)num72, (int)num73);
								}

								if (player.gravDir == -1f)
								{
									float num77 = Main.combatText[n].position.Y - Main.screenPosition.Y;
									num77 = Main.screenHeight - num77;
									Main.spriteBatch.DrawString(Main.fontCombatText[num68], Main.combatText[n].text, new Vector2(Main.combatText[n].position.X - Main.screenPosition.X + num75 + origin.X, num77 + num76 + origin.Y), color9, Main.combatText[n].rotation, origin, Main.combatText[n].scale, SpriteEffects.None, 0f);
								}
								else
								{
									Main.spriteBatch.DrawString(Main.fontCombatText[num68], Main.combatText[n].text, new Vector2(Main.combatText[n].position.X - Main.screenPosition.X + num75 + origin.X, Main.combatText[n].position.Y - Main.screenPosition.Y + num76 + origin.Y), color9, Main.combatText[n].rotation, origin, Main.combatText[n].scale, SpriteEffects.None, 0f);
								}
							}
						}
					}

					num67 = ItemText.TargetScale;
					if (num67 == 0f)
					{
						num67 = 1f;
					}

					for (int num78 = 0; num78 < Main.itemText.Length; num78++)
					{
						if (Main.itemText[num78].active)
						{
							string text = Main.itemText[num78].name + (Main.itemText[num78].stack > 1 ? $" ({Main.itemText[num78].stack})" : "");

							Vector2 vector7 = Main.fontMouseText.MeasureString(text);
							Vector2 origin2 = new Vector2(vector7.X * 0.5f, vector7.Y * 0.5f);
							float num79 = Main.itemText[num78].scale / num67;
							float num80 = Main.itemText[num78].color.R;
							float num81 = Main.itemText[num78].color.G;
							float num82 = Main.itemText[num78].color.B;
							float num83 = Main.itemText[num78].color.A;
							num80 *= num79 * Main.itemText[num78].alpha * 0.3f;
							num82 *= num79 * Main.itemText[num78].alpha * 0.3f;
							num81 *= num79 * Main.itemText[num78].alpha * 0.3f;
							num83 *= num79 * Main.itemText[num78].alpha;
							Color color10 = new Color((int)num80, (int)num81, (int)num82, (int)num83);
							for (int num84 = 0; num84 < 5; num84++)
							{
								float num85 = 0f;
								float num86 = 0f;
								if (num84 == 0)
								{
									num85 -= num67 * 2f;
								}
								else if (num84 == 1)
								{
									num85 += num67 * 2f;
								}
								else if (num84 == 2)
								{
									num86 -= num67 * 2f;
								}
								else if (num84 == 3)
								{
									num86 += num67 * 2f;
								}
								else
								{
									num80 = Main.itemText[num78].color.R * num79 * Main.itemText[num78].alpha;
									num82 = Main.itemText[num78].color.B * num79 * Main.itemText[num78].alpha;
									num81 = Main.itemText[num78].color.G * num79 * Main.itemText[num78].alpha;
									num83 = Main.itemText[num78].color.A * num79 * Main.itemText[num78].alpha;
									color10 = new Color((int)num80, (int)num81, (int)num82, (int)num83);
								}

								if (num84 < 4)
								{
									num83 = Main.itemText[num78].color.A * num79 * Main.itemText[num78].alpha;
									color10 = new Color(0, 0, 0, (int)num83);
								}

								float num87 = Main.itemText[num78].position.Y - Main.screenPosition.Y + num86;
								if (player.gravDir == -1f)
								{
									num87 = Main.screenHeight - num87;
								}

								Main.spriteBatch.DrawString(Main.fontMouseText, text, new Vector2(Main.itemText[num78].position.X - Main.screenPosition.X + num85 + origin2.X, num87 + origin2.Y), color10, Main.itemText[num78].rotation, origin2, Main.itemText[num78].scale, SpriteEffects.None, 0f);
							}
						}
					}

					if (Main.netMode == 1 && !string.IsNullOrEmpty(Netplay.Connection.StatusText))
					{
						string text2 = string.Concat(Netplay.Connection.StatusText, ": ", (int)(Netplay.Connection.StatusCount / (float)Netplay.Connection.StatusMax * 100f), "%");
						Main.spriteBatch.DrawString(Main.fontMouseText, text2, new Vector2(628f - Main.fontMouseText.MeasureString(text2).X * 0.5f + (Main.screenWidth - 800), 84f), new Color(Main.mouseTextColor, Main.mouseTextColor, Main.mouseTextColor, Main.mouseTextColor), 0f, default(Vector2), 1f, SpriteEffects.None, 0f);
					}

					if (Main.BlackFadeIn > 0)
					{
						if (Main.BlackFadeIn < 0)
						{
							Main.BlackFadeIn = 0;
						}

						int num88 = Main.BlackFadeIn;
						if (num88 > 255)
						{
							num88 = 255;
						}

						Main.BlackFadeIn -= 25;
						Main.spriteBatch.Draw(Main.loTexture, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), new Color(0, 0, 0, num88));
					}

					Main.spriteBatch.End();
					Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, null, null, null, Main.UIScaleMatrix);
					PlayerInput.SetZoom_UI();
					DrawFPS();
					Main.spriteBatch.End();

					if (!Main.mapFullscreen)
					{
						if (Main.ignoreErrors)
						{
							try
							{
								DrawInterface(gameTime);

								goto IL_4EB3;
							}
							catch (Exception e5)
							{
								TimeLogger.DrawException(e5);
								goto IL_4EB3;
							}
						}

						DrawInterface(gameTime);
					}

				IL_4EB3:
					TimeLogger.DetailedDrawTime(27);
				}
				else
				{
					Main.maxQ = true;
				}

				TimeLogger.DetailedDrawTime(37);
				Main.mouseLeftRelease = !Main.mouseLeft;

				Main.mouseRightRelease = !Main.mouseRight;

				Main.mouseMiddleRelease = !Main.mouseMiddle;

				Main.mouseXButton1Release = !Main.mouseXButton1;

				Main.mouseXButton2Release = !Main.mouseXButton2;

				if (!PlayerInput.Triggers.Current.MouseRight)
				{
					Main.stackSplit = 0;
				}

				if (Main.stackSplit > 0)
				{
					Main.stackSplit--;
				}

				TimeLogger.RenderTime(Main.renderCount, stopwatch.Elapsed.TotalMilliseconds);
				TimeLogger.EndDrawFrame();
			}

			if (player.talkNPC >= 0 || player.sign >= 0 || Main.playerInventory && !CaptureManager.Instance.Active)
			{
				//player.ToggleInv();
			}

			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, null, null, null, Main.UIScaleMatrix);
			PlayerInput.SetZoom_UI();
			DrawFPS();
			DrawPlayerChat();
			PlayerInput.SetZoom_Unscaled();
			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, null, null, null);
			TimeLogger.MapDrawTime(stopwatch.Elapsed.TotalMilliseconds);
			TimeLogger.EndDrawFrame();
			PlayerInput.SetDesiredZoomContext(ZoomContext.Unscaled);
			CaptureManager.Instance.Update();
			if (CaptureManager.Instance.Active) CaptureManager.Instance.Draw(Main.spriteBatch);

			Main.spriteBatch.End();
			Main.mouseLeftRelease = !Main.mouseLeft;
		}

		private static void DrawStars()
		{
			if (Main.screenPosition.Y < Main.worldSurface * 16.0 + 16.0 && 255f * (1f - Main.cloudAlpha) - Main.bgColor.R - 25f > 0f && Main.netMode != 2)
			{
				for (int j = 0; j < Main.numStars; j++)
				{
					Color color = Color.Transparent;
					float num56 = Main.evilTiles / 500f;
					if (num56 > 1f)
					{
						num56 = 1f;
					}

					num56 = 1f - num56 * 0.5f;
					if (Main.evilTiles <= 0)
					{
						num56 = 1f;
					}

					int num57 = (int)((255 - Main.bgColor.R - 100) * Main.star[j].twinkle * num56);
					int num58 = (int)((255 - Main.bgColor.G - 100) * Main.star[j].twinkle * num56);
					int num59 = (int)((255 - Main.bgColor.B - 100) * Main.star[j].twinkle * num56);
					if (num57 < 0)
					{
						num57 = 0;
					}

					if (num58 < 0)
					{
						num58 = 0;
					}

					if (num59 < 0)
					{
						num59 = 0;
					}

					color.R = (byte)num57;
					color.G = (byte)(num58 * num56);
					color.B = (byte)(num59 * num56);
					float num60 = Main.star[j].position.X * (Main.screenWidth / 800f);
					float num61 = Main.star[j].position.Y * (Main.screenHeight / 600f);
					Main.spriteBatch.Draw(Main.starTexture[Main.star[j].type], new Vector2(num60 + Main.starTexture[Main.star[j].type].Width * 0.5f, num61 + Main.starTexture[Main.star[j].type].Height * 0.5f + bgTop), new Rectangle(0, 0, Main.starTexture[Main.star[j].type].Width, Main.starTexture[Main.star[j].type].Height), color, Main.star[j].rotation, new Vector2(Main.starTexture[Main.star[j].type].Width * 0.5f, Main.starTexture[Main.star[j].type].Height * 0.5f), Main.star[j].scale * Main.star[j].twinkle, SpriteEffects.None, 0f);
				}
			}
		}

		private static void HandleSunAndMoon(Player player, int sunPosX, int sunPosY, Color sunColor, float sunScale, float sunRotation, int moonPosX, int moonPosY, Color moonColor, float moonScale, float moonRotation)
		{
			if (Main.screenPosition.Y / 16f < Main.worldSurface + 2.0)
			{
				if (Main.dayTime)
				{
					sunScale *= 1.1f;
					if (Main.eclipse)
					{
						float num62 = 1f - Main.shroomLight;
						num62 -= Main.cloudAlpha * 1.5f;
						if (num62 < 0f)
						{
							num62 = 0f;
						}

						Color color2 = new Color((byte)(255f * num62), (byte)(sunColor.G * num62), (byte)(sunColor.B * num62), (byte)(255f * num62));
						Color color3 = new Color((byte)(sunColor.R * num62), (byte)(sunColor.G * num62), (byte)(sunColor.B * num62), (byte)((sunColor.B - 60) * num62));
						Main.spriteBatch.Draw(Main.sun3Texture, new Vector2(sunPosX, sunPosY + Main.sunModY), new Rectangle(0, 0, Main.sun3Texture.Width, Main.sun3Texture.Height), color2, sunRotation, new Vector2(Main.sun3Texture.Width * 0.5f, Main.sun3Texture.Height * 0.5f), sunScale, SpriteEffects.None, 0f);
						Main.spriteBatch.Draw(Main.sun3Texture, new Vector2(sunPosX, sunPosY + Main.sunModY), new Rectangle(0, 0, Main.sun3Texture.Width, Main.sun3Texture.Height), color3, sunRotation, new Vector2(Main.sun3Texture.Width * 0.5f, Main.sun3Texture.Height * 0.5f), sunScale, SpriteEffects.None, 0f);
					}
					else if (!Main.gameMenu && player.head == 12)
					{
						float num63 = 1f - Main.shroomLight;
						num63 -= Main.cloudAlpha * 1.5f;
						if (num63 < 0f)
						{
							num63 = 0f;
						}

						Color color4 = new Color((byte)(255f * num63), (byte)(sunColor.G * num63), (byte)(sunColor.B * num63), (byte)(255f * num63));
						Color color5 = new Color((byte)(sunColor.R * num63), (byte)(sunColor.G * num63), (byte)(sunColor.B * num63), (byte)((sunColor.B - 60) * num63));
						Main.spriteBatch.Draw(Main.sun2Texture, new Vector2(sunPosX, sunPosY + Main.sunModY), new Rectangle(0, 0, Main.sun2Texture.Width, Main.sun2Texture.Height), color4, sunRotation, new Vector2(Main.sun2Texture.Width * 0.5f, Main.sun2Texture.Height * 0.5f), sunScale, SpriteEffects.None, 0f);
						Main.spriteBatch.Draw(Main.sun2Texture, new Vector2(sunPosX, sunPosY + Main.sunModY), new Rectangle(0, 0, Main.sun2Texture.Width, Main.sun2Texture.Height), color5, sunRotation, new Vector2(Main.sun2Texture.Width * 0.5f, Main.sun2Texture.Height * 0.5f), sunScale, SpriteEffects.None, 0f);
					}
					else
					{
						float num64 = 1f - Main.shroomLight;
						num64 -= Main.cloudAlpha * 1.5f;
						if (num64 < 0f)
						{
							num64 = 0f;
						}

						Color color6 = new Color((byte)(255f * num64), (byte)(sunColor.G * num64), (byte)(sunColor.B * num64), (byte)(255f * num64));
						Color color7 = new Color((byte)(sunColor.R * num64), (byte)(sunColor.G * num64), (byte)(sunColor.B * num64), (byte)(sunColor.B * num64));
						Main.spriteBatch.Draw(Main.sunTexture, new Vector2(sunPosX, sunPosY + Main.sunModY), new Rectangle(0, 0, Main.sunTexture.Width, Main.sunTexture.Height), color6, sunRotation, new Vector2(Main.sunTexture.Width * 0.5f, Main.sunTexture.Height * 0.5f), sunScale, SpriteEffects.None, 0f);
						Main.spriteBatch.Draw(Main.sunTexture, new Vector2(sunPosX, sunPosY + Main.sunModY), new Rectangle(0, 0, Main.sunTexture.Width, Main.sunTexture.Height), color7, sunRotation, new Vector2(Main.sunTexture.Width * 0.5f, Main.sunTexture.Height * 0.5f), sunScale, SpriteEffects.None, 0f);
					}
				}

				if (!Main.dayTime)
				{
					float num65 = 1f - Main.cloudAlpha * 1.5f;
					if (num65 < 0f)
					{
						num65 = 0f;
					}

					moonColor.R = (byte)(moonColor.R * num65);
					moonColor.G = (byte)(moonColor.G * num65);
					moonColor.B = (byte)(moonColor.B * num65);
					moonColor.A = (byte)(moonColor.A * num65);
					if (Main.pumpkinMoon)
					{
						Main.spriteBatch.Draw(Main.pumpkinMoonTexture, new Vector2(moonPosX, moonPosY + Main.moonModY), new Rectangle(0, Main.pumpkinMoonTexture.Width * Main.moonPhase, Main.pumpkinMoonTexture.Width, Main.pumpkinMoonTexture.Width), moonColor, moonRotation, new Vector2(Main.pumpkinMoonTexture.Width * 0.5f, Main.pumpkinMoonTexture.Width * 0.5f), moonScale, SpriteEffects.None, 0f);
					}
					else if (Main.snowMoon)
					{
						Main.spriteBatch.Draw(Main.snowMoonTexture, new Vector2(moonPosX, moonPosY + Main.moonModY), new Rectangle(0, Main.snowMoonTexture.Width * Main.moonPhase, Main.snowMoonTexture.Width, Main.snowMoonTexture.Width), moonColor, moonRotation, new Vector2(Main.snowMoonTexture.Width * 0.5f, Main.snowMoonTexture.Width * 0.5f), moonScale, SpriteEffects.None, 0f);
					}
					else
					{
						Main.spriteBatch.Draw(Main.moonTexture[Main.moonType], new Vector2(moonPosX, moonPosY + Main.moonModY), new Rectangle(0, Main.moonTexture[Main.moonType].Width * Main.moonPhase, Main.moonTexture[Main.moonType].Width, Main.moonTexture[Main.moonType].Width), moonColor, moonRotation, new Vector2(Main.moonTexture[Main.moonType].Width * 0.5f, Main.moonTexture[Main.moonType].Width * 0.5f), moonScale, SpriteEffects.None, 0f);
					}
				}
			}

			Rectangle sunRect;
			if (Main.dayTime)
			{
				sunRect = new Rectangle((int)(sunPosX - Main.sunTexture.Width * 0.5 * sunScale), (int)(sunPosY - Main.sunTexture.Height * 0.5 * sunScale + Main.sunModY), (int)(Main.sunTexture.Width * sunScale), (int)(Main.sunTexture.Width * sunScale));
			}
			else
			{
				sunRect = new Rectangle((int)(moonPosX - Main.moonTexture[Main.moonType].Width * 0.5 * moonScale), (int)(moonPosY - Main.moonTexture[Main.moonType].Width * 0.5 * moonScale + Main.moonModY), (int)(Main.moonTexture[Main.moonType].Width * moonScale), (int)(Main.moonTexture[Main.moonType].Width * moonScale));
			}

			Rectangle rectangle = new Rectangle(Main.mouseX, Main.mouseY, 1, 1);
			Main.sunModY = (short)(Main.sunModY * 0.999);
			Main.moonModY = (short)(Main.moonModY * 0.999);
			if (Main.gameMenu && Main.netMode != NetmodeID.MultiplayerClient)
			{
				if (Main.mouseLeft && Main.hasFocus)
				{
					if (rectangle.Intersects(sunRect) || Main.grabSky)
					{
						if (Main.dayTime)
						{
							Main.time = 54000.0 * ((Main.mouseX + Main.sunTexture.Width) / (Main.screenWidth + (float)(Main.sunTexture.Width * 2)));
							Main.sunModY = (short)(Main.mouseY - sunPosY);
							if (Main.time > 53990.0)
							{
								Main.time = 53990.0;
							}
						}
						else
						{
							Main.time = 32400.0 * ((Main.mouseX + Main.moonTexture[Main.moonType].Width) / (Main.screenWidth + (float)(Main.moonTexture[Main.moonType].Width * 2)));
							Main.moonModY = (short)(Main.mouseY - moonPosY);
							if (Main.time > 32390.0) Main.time = 32390.0;
						}

						if (Main.time < 10.0) Main.time = 10.0;

						if (Main.netMode != NetmodeID.SinglePlayer) NetMessage.SendData(MessageID.MenuSunMoon);

						Main.grabSky = true;
					}
				}
				else
				{
					Main.grabSky = false;
				}
			}
		}

		private static void HandleBGColor(ref Color white, ref Color white2)
		{
			if (Main.dayTime)
			{
				if (Main.time < 13500.0)
				{
					float num28 = (float)(Main.time / 13500.0);
					white.R = (byte)(num28 * 200f + 55f);
					white.G = (byte)(num28 * 180f + 75f);
					white.B = (byte)(num28 * 250f + 5f);
					Main.bgColor.R = (byte)(num28 * 230f + 25f);
					Main.bgColor.G = (byte)(num28 * 220f + 35f);
					Main.bgColor.B = (byte)(num28 * 220f + 35f);
				}

				if (Main.time > 45900.0)
				{
					float num28 = (float)(1.0 - (Main.time / 54000.0 - 0.85) * 6.666666666666667);
					white.R = (byte)(num28 * 120f + 55f);
					white.G = (byte)(num28 * 100f + 25f);
					white.B = (byte)(num28 * 120f + 55f);
					Main.bgColor.R = (byte)(num28 * 200f + 35f);
					Main.bgColor.G = (byte)(num28 * 85f + 35f);
					Main.bgColor.B = (byte)(num28 * 135f + 35f);
				}
				else if (Main.time > 37800.0)
				{
					float num28 = (float)(1.0 - (Main.time / 54000.0 - 0.7) * 6.666666666666667);
					white.R = (byte)(num28 * 80f + 175f);
					white.G = (byte)(num28 * 130f + 125f);
					white.B = (byte)(num28 * 100f + 155f);
					Main.bgColor.R = (byte)(num28 * 20f + 235f);
					Main.bgColor.G = (byte)(num28 * 135f + 120f);
					Main.bgColor.B = (byte)(num28 * 85f + 170f);
				}
			}
			else
			{
				if (Main.bloodMoon)
				{
					if (Main.time < 16200.0)
					{
						float num28 = (float)(1.0 - Main.time / 16200.0);
						white2.R = (byte)(num28 * 10f + 205f);
						white2.G = (byte)(num28 * 170f + 55f);
						white2.B = (byte)(num28 * 200f + 55f);
						Main.bgColor.R = (byte)(40f - num28 * 40f + 35f);
						Main.bgColor.G = (byte)(num28 * 20f + 15f);
						Main.bgColor.B = (byte)(num28 * 20f + 15f);
					}
					else if (Main.time >= 16200.0)
					{
						float num28 = (float)((Main.time / 32400.0 - 0.5) * 2.0);
						white2.R = (byte)(num28 * 50f + 205f);
						white2.G = (byte)(num28 * 100f + 155f);
						white2.B = (byte)(num28 * 100f + 155f);
						white2.R = (byte)(num28 * 10f + 205f);
						white2.G = (byte)(num28 * 170f + 55f);
						white2.B = (byte)(num28 * 200f + 55f);
						Main.bgColor.R = (byte)(40f - num28 * 40f + 35f);
						Main.bgColor.G = (byte)(num28 * 20f + 15f);
						Main.bgColor.B = (byte)(num28 * 20f + 15f);
					}
				}
				else if (Main.time < 16200.0)
				{
					float num28 = (float)(1.0 - Main.time / 16200.0);
					white2.R = (byte)(num28 * 10f + 205f);
					white2.G = (byte)(num28 * 70f + 155f);
					white2.B = (byte)(num28 * 100f + 155f);
					Main.bgColor.R = (byte)(num28 * 30f + 5f);
					Main.bgColor.G = (byte)(num28 * 30f + 5f);
					Main.bgColor.B = (byte)(num28 * 30f + 5f);
				}
				else if (Main.time >= 16200.0)
				{
					float num28 = (float)((Main.time / 32400.0 - 0.5) * 2.0);
					white2.R = (byte)(num28 * 50f + 205f);
					white2.G = (byte)(num28 * 100f + 155f);
					white2.B = (byte)(num28 * 100f + 155f);
					Main.bgColor.R = (byte)(num28 * 20f + 5f);
					Main.bgColor.G = (byte)(num28 * 30f + 5f);
					Main.bgColor.B = (byte)(num28 * 30f + 5f);
				}
			}
		}

		public static int lastTileY
		{
			get => Main.instance.GetValue<int>("lastTileY");
			set => Main.instance.SetValue("lastTileY", value);
		}

		public static int firstTileY
		{
			get => Main.instance.GetValue<int>("firstTileY");
			set => Main.instance.SetValue("firstTileY", value);
		}

		public static int lastTileX
		{
			get => Main.instance.GetValue<int>("lastTileX");
			set => Main.instance.SetValue("lastTileX", value);
		}

		public static int firstTileX
		{
			get => Main.instance.GetValue<int>("firstTileX");
			set => Main.instance.SetValue("firstTileX", value);
		}

		public static float atmo
		{
			get => typeof(Main).GetValue<float>("atmo");
			set => typeof(Main).SetValue("atmo", value);
		}

		public static int bgLoopsY
		{
			get => Main.instance.GetValue<int>("bgLoopsY");
			set => Main.instance.SetValue("bgLoopsY", value);
		}

		public static int bgStartY
		{
			get => Main.instance.GetValue<int>("bgStartY");
			set => Main.instance.SetValue("bgStartY", value);
		}

		public static int bgLoops
		{
			get => Main.instance.GetValue<int>("bgLoops");
			set => Main.instance.SetValue("bgLoops", value);
		}

		public static int bgStart
		{
			get => Main.instance.GetValue<int>("bgStart");
			set => Main.instance.SetValue("bgStart", value);
		}

		public static double bgParallax
		{
			get => Main.instance.GetValue<double>("bgParallax");
			set => Main.instance.SetValue("bgParallax", value);
		}

		public static int bgTop
		{
			get => Main.instance.GetValue<int>("bgTop");
			set => Main.instance.SetValue("bgTop", value);
		}

		private static void HandleDrawToMap()
		{
			if (!Main.loadMap)
			{
				void DrawToMap_Section(int secX, int secY) => Main.instance.InvokeMethod<object>("DrawToMap_Section", secX, secY);

				if (!Main.gameMenu)
				{
					TimeLogger.DetailedDrawReset();
					Stopwatch stopwatch2 = Stopwatch.StartNew();
					while (stopwatch2.ElapsedMilliseconds < 5L && Main.sectionManager.GetNextMapDraw(Main.player[Main.myPlayer].position, out int secX, out int secY))
						DrawToMap_Section(secX, secY);
					TimeLogger.DetailedDrawTime(3);
				}

				if (Main.updateMap)
				{
					if (Main.instance.IsActive || Main.netMode == 1)
					{
						if (Main.refreshMap)
						{
							Main.refreshMap = false;
							Main.sectionManager.ClearMapDraw();
						}

						DrawToMap(Main.instance);
						Main.updateMap = false;
					}
					else if (MapHelper.numUpdateTile > 0) DrawToMap(Main.instance);

					TimeLogger.DetailedDrawTime(4);
				}
			}
		}

		private static void RenderToTargets(GameTime gameTime)
		{
			TimeLogger.DetailedDrawReset();
			if (!Main.gameMenu)
			{
				void RenderTiles() => Main.instance.InvokeMethod<object>("RenderTiles");
				void RenderBackground() => Main.instance.InvokeMethod<object>("RenderBackground");
				void RenderWalls() => Main.instance.InvokeMethod<object>("RenderWalls");
				void RenderTiles2() => Main.instance.InvokeMethod<object>("RenderTiles2");
				void RenderWater() => Main.instance.InvokeMethod<object>("RenderWater");

				DrawToInterface(gameTime);

				Main.instance.waterfallManager.FindWaterfalls();
				TimeLogger.DetailedDrawTime(2);
				if (Main.renderNow)
				{
					Main.screenLastPosition = Main.screenPosition;
					Main.renderNow = false;
					Main.renderCount = 99;
					Main.instance.InvokeMethod<object>("Draw", gameTime);

					Lighting.LightTiles(firstTileX, lastTileX, firstTileY, lastTileY);
					Lighting.LightTiles(firstTileX, lastTileX, firstTileY, lastTileY);
					RenderTiles();
					Main.sceneTilePos.X = Main.screenPosition.X - Main.offScreenRange;
					Main.sceneTilePos.Y = Main.screenPosition.Y - Main.offScreenRange;
					RenderBackground();
					Main.sceneBackgroundPos.X = Main.screenPosition.X - Main.offScreenRange;
					Main.sceneBackgroundPos.Y = Main.screenPosition.Y - Main.offScreenRange;
					RenderWalls();
					Main.sceneWallPos.X = Main.screenPosition.X - Main.offScreenRange;
					Main.sceneWallPos.Y = Main.screenPosition.Y - Main.offScreenRange;
					RenderTiles2();
					Main.sceneTile2Pos.X = Main.screenPosition.X - Main.offScreenRange;
					Main.sceneTile2Pos.Y = Main.screenPosition.Y - Main.offScreenRange;
					RenderWater();
					Main.sceneWaterPos.X = Main.screenPosition.X - Main.offScreenRange;
					Main.sceneWaterPos.Y = Main.screenPosition.Y - Main.offScreenRange;
					Main.renderCount = 99;
				}
				else
				{
					if (Main.renderCount == 3)
					{
						RenderTiles();
						Main.sceneTilePos.X = Main.screenPosition.X - Main.offScreenRange;
						Main.sceneTilePos.Y = Main.screenPosition.Y - Main.offScreenRange;
					}

					if (Main.renderCount == 3)
					{
						RenderTiles2();
						Main.sceneTile2Pos.X = Main.screenPosition.X - Main.offScreenRange;
						Main.sceneTile2Pos.Y = Main.screenPosition.Y - Main.offScreenRange;
					}

					if (Main.renderCount == 3)
					{
						RenderWalls();
						Main.sceneWallPos.X = Main.screenPosition.X - Main.offScreenRange;
						Main.sceneWallPos.Y = Main.screenPosition.Y - Main.offScreenRange;
					}

					if (Main.renderCount == 2)
					{
						RenderBackground();
						Main.sceneBackgroundPos.X = Main.screenPosition.X - Main.offScreenRange;
						Main.sceneBackgroundPos.Y = Main.screenPosition.Y - Main.offScreenRange;
					}

					if (Main.renderCount == 1)
					{
						RenderWater();
						Main.sceneWaterPos.X = Main.screenPosition.X - Main.offScreenRange;
						Main.sceneWaterPos.Y = Main.screenPosition.Y - Main.offScreenRange;
					}
				}

				if (Main.render)
				{
					if (Math.Abs(Main.sceneTilePos.X - (Main.screenPosition.X - Main.offScreenRange)) > Main.offScreenRange || Math.Abs(Main.sceneTilePos.Y - (Main.screenPosition.Y - Main.offScreenRange)) > Main.offScreenRange)
					{
						RenderTiles();
						Main.sceneTilePos.X = Main.screenPosition.X - Main.offScreenRange;
						Main.sceneTilePos.Y = Main.screenPosition.Y - Main.offScreenRange;
					}

					if (Math.Abs(Main.sceneTile2Pos.X - (Main.screenPosition.X - Main.offScreenRange)) > Main.offScreenRange || Math.Abs(Main.sceneTile2Pos.Y - (Main.screenPosition.Y - Main.offScreenRange)) > Main.offScreenRange)
					{
						RenderTiles2();
						Main.sceneTile2Pos.X = Main.screenPosition.X - Main.offScreenRange;
						Main.sceneTile2Pos.Y = Main.screenPosition.Y - Main.offScreenRange;
					}

					if (Math.Abs(Main.sceneBackgroundPos.X - (Main.screenPosition.X - Main.offScreenRange)) > Main.offScreenRange || Math.Abs(Main.sceneBackgroundPos.Y - (Main.screenPosition.Y - Main.offScreenRange)) > Main.offScreenRange)
					{
						RenderBackground();
						Main.sceneBackgroundPos.X = Main.screenPosition.X - Main.offScreenRange;
						Main.sceneBackgroundPos.Y = Main.screenPosition.Y - Main.offScreenRange;
					}

					if (Math.Abs(Main.sceneWallPos.X - (Main.screenPosition.X - Main.offScreenRange)) > Main.offScreenRange || Math.Abs(Main.sceneWallPos.Y - (Main.screenPosition.Y - Main.offScreenRange)) > Main.offScreenRange)
					{
						RenderWalls();
						Main.sceneWallPos.X = Main.screenPosition.X - Main.offScreenRange;
						Main.sceneWallPos.Y = Main.screenPosition.Y - Main.offScreenRange;
					}

					if (Math.Abs(Main.sceneWaterPos.X - (Main.screenPosition.X - Main.offScreenRange)) > Main.offScreenRange || Math.Abs(Main.sceneWaterPos.Y - (Main.screenPosition.Y - Main.offScreenRange)) > Main.offScreenRange)
					{
						RenderWater();
						Main.sceneWaterPos.X = Main.screenPosition.X - Main.offScreenRange;
						Main.sceneWaterPos.Y = Main.screenPosition.Y - Main.offScreenRange;
					}
				}
			}
		}

		private static void HandleCamera()
		{
			if (!Main.gameMenu && Main.netMode != NetmodeID.Server)
			{
				Player player = Main.LocalPlayer;

				if (Main.cameraX != 0f && !player.pulley) Main.cameraX = 0f;

				if (Main.cameraX > 0f)
				{
					Main.cameraX -= 1f;
					if (Main.cameraX < 0f) Main.cameraX = 0f;
				}

				if (Main.cameraX < 0f)
				{
					Main.cameraX += 1f;
					if (Main.cameraX > 0f) Main.cameraX = 0f;
				}

				Vector2 screenPosition = Main.screenPosition;
				Main.screenPosition.X = player.Center.X - Main.screenWidth * 0.5f + Main.cameraX;
				Main.screenPosition.Y = player.Bottom.Y - 21 - Main.screenHeight * 0.5f + player.gfxOffY;
				float zoomX = 0f;
				float zoomY = 0f;
				float offset = 24f;
				if (player.noThrow <= 0 && !player.lastMouseInterface || Main.zoomX != 0f || Main.zoomY != 0f)
				{
					if (PlayerInput.UsingGamepad)
					{
						if (PlayerInput.GamepadThumbstickRight.Length() != 0f || !Main.SmartCursorEnabled)
						{
							float zoom = -1f;
							if (player.inventory[player.selectedItem].type == 1254 && player.scope)
							{
								zoom = 0.8f;
							}
							else if (player.inventory[player.selectedItem].type == 1254)
							{
								zoom = 0.6666667f;
							}
							else if (player.inventory[player.selectedItem].type == 1299)
							{
								zoom = 0.6666667f;
							}
							else if (player.scope)
							{
								zoom = 0.5f;
							}

							PlayerHooks.ModifyZoom(player, ref zoom);
							Vector2 vector3 = (Main.MouseScreen - new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f) / (new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f);
							offset = 48f;
							if (vector3 != Vector2.Zero && zoom != -1f)
							{
								Vector2 vector4 = new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f * vector3 * zoom;
								zoomX = vector4.X;
								zoomY = vector4.Y;
							}
						}
					}
					else if (player.inventory[player.selectedItem].type == 1254 && player.scope && Main.mouseRight)
					{
						int mouseX = Main.mouseX.Clamp(0, Main.screenWidth);
						int mouseY = Main.mouseY.Clamp(0, Main.screenHeight);

						float zoom = 0.8f;
						PlayerHooks.ModifyZoom(player, ref zoom);
						zoomX = (mouseX - Main.screenWidth * 0.5f) * zoom;
						zoomY = (mouseY - Main.screenHeight * 0.5f) * zoom;
					}
					else if (player.inventory[player.selectedItem].type == 1254 && Main.mouseRight)
					{
						int mouseX = Main.mouseX.Clamp(0, Main.screenWidth);
						int mouseY = Main.mouseY.Clamp(0, Main.screenHeight);

						float zoom = 0.6666667f;
						PlayerHooks.ModifyZoom(player, ref zoom);
						zoomX = (mouseX - Main.screenWidth * 0.5f) * zoom;
						zoomY = (mouseY - Main.screenHeight * 0.5f) * zoom;
					}
					else if (player.inventory[player.selectedItem].type == 1299 && player.selectedItem != 58)
					{
						int mouseX = Main.mouseX.Clamp(0, Main.screenWidth);
						int mouseY = Main.mouseY.Clamp(0, Main.screenHeight);

						float zoom = 0.6666667f;
						PlayerHooks.ModifyZoom(player, ref zoom);
						zoomX = (mouseX - Main.screenWidth * 0.5f) * zoom;
						zoomY = (mouseY - Main.screenHeight * 0.5f) * zoom;
					}
					else if (player.scope && Main.mouseRight)
					{
						int mouseX = Main.mouseX.Clamp(0, Main.screenWidth);
						int mouseY = Main.mouseY.Clamp(0, Main.screenHeight);

						float zoom = 0.5f;
						PlayerHooks.ModifyZoom(player, ref zoom);
						zoomX = (mouseX - Main.screenWidth * 0.5f) * zoom;
						zoomY = (mouseY - Main.screenHeight * 0.5f) * zoom;
					}
					else
					{
						int mouseX = Main.mouseX.Clamp(0, Main.screenWidth);
						int mouseY = Main.mouseY.Clamp(0, Main.screenHeight);

						float zoom = -1f;
						PlayerHooks.ModifyZoom(player, ref zoom);
						if (zoom != -1f)
						{
							zoomX = (mouseX - Main.screenWidth * 0.5f) * zoom;
							zoomY = (mouseY - Main.screenHeight * 0.5f) * zoom;
						}
					}
				}

				if (float.IsNaN(Main.zoomX)) Main.zoomX = 0f;
				if (float.IsNaN(Main.zoomY)) Main.zoomY = 0f;

				float zoomXDelta = zoomX - Main.zoomX;
				float zoomYDelta = zoomY - Main.zoomY;
				float zoomDistance = (float)Math.Sqrt(zoomXDelta * zoomXDelta + zoomYDelta * zoomYDelta);
				if (zoomDistance < offset)
				{
					Main.zoomX = zoomX;
					Main.zoomY = zoomY;
				}
				else
				{
					zoomDistance = offset / zoomDistance;
					zoomXDelta *= zoomDistance;
					zoomYDelta *= zoomDistance;
					Main.zoomX += zoomXDelta;
					Main.zoomY += zoomYDelta;
				}

				Main.screenPosition.X = Main.screenPosition.X + Main.zoomX;
				Main.screenPosition.Y = Main.screenPosition.Y + Main.zoomY * player.gravDir;
				if (typeof(Main).GetValue<float>("cameraLerp") > 0f)
				{
					float num18 = Vector2.Distance(screenPosition, Main.screenPosition) - player.velocity.Length();
					if (num18 < 0.25f || typeof(Main).GetValue<bool>("cameraGamePadLerp") && !PlayerInput.UsingGamepad)
					{
						typeof(Main).SetValue("cameraLerp", 0f);
						typeof(Main).SetValue("cameraGamePadLerp", false);
					}
					else
					{
						Main.screenPosition = Vector2.Lerp(screenPosition, Main.screenPosition, typeof(Main).GetValue<float>("cameraLerp"));
					}
				}

				Main.screenPosition.X = (int)Main.screenPosition.X;
				Main.screenPosition.Y = (int)Main.screenPosition.Y;
				PlayerHooks.ModifyScreenPosition(player);

				typeof(Main).InvokeMethod<object>("ClampScreenPositionToWorld");
			}
		}

		private static void HandleStackSplit()
		{
			if (Main.stackSplit == 0)
			{
				Main.stackCounter = 0;
				Main.stackDelay = 7;
				Main.superFastStack = 0;
			}
			else
			{
				Main.stackCounter++;
				int num = 30;
				if (num == 7)
				{
					num = 30;
				}
				else if (Main.stackDelay == 6)
				{
					num = 25;
				}
				else if (Main.stackDelay == 5)
				{
					num = 20;
				}
				else if (Main.stackDelay == 4)
				{
					num = 15;
				}
				else if (Main.stackDelay == 3)
				{
					num = 10;
				}
				else
				{
					num = 5;
				}

				if (Main.stackCounter >= num)
				{
					Main.stackDelay--;
					if (Main.stackDelay < 2)
					{
						Main.stackDelay = 2;
						Main.superFastStack++;
					}

					Main.stackCounter = 0;
				}
			}
		}

		public static RenderTarget2D uiTarget;

		public static void EnsureRenderTargetContent(On.Terraria.Main.orig_EnsureRenderTargetContent orig, Main self)
		{
			if (Main.waterTarget == null || Main.waterTarget.IsContentLost || self.backWaterTarget == null || self.backWaterTarget.IsContentLost || self.blackTarget == null || self.blackTarget.IsContentLost || self.tileTarget == null || self.tileTarget.IsContentLost || self.tile2Target == null || self.tile2Target.IsContentLost || self.wallTarget == null || self.wallTarget.IsContentLost || self.backgroundTarget == null || self.backgroundTarget.IsContentLost || Main.screenTarget == null || Main.screenTarget.IsContentLost || Main.screenTargetSwap == null || Main.screenTargetSwap.IsContentLost || uiTarget == null || uiTarget.IsContentLost)
			{
				InitTargets();
			}
		}

		private static Type ModHooks = Assembly.GetAssembly(typeof(ModLoader)).GetType("Terraria.ModLoader.ModHooks");

		private static void DrawToInterface(GameTime gameTime)
		{
			Main.graphics.GraphicsDevice.SetRenderTarget(uiTarget);
			Main.graphics.GraphicsDevice.Clear(Color.Transparent);

			Main._drawInterfaceGameTime = gameTime;
			if (Main.instance.GetValue<bool>("_needToSetupDrawInterfaceLayers"))
			{
				Main.instance.InvokeMethod<object>("SetupDrawInterfaceLayers");
			}

			PlayerInput.SetZoom_UI();
			List<GameInterfaceLayer> interfaceLayers = new List<GameInterfaceLayer>(Main.instance.GetValue<IEnumerable<GameInterfaceLayer>>("_gameInterfaceLayers"));
			ModHooks.InvokeMethod<object>("ModifyInterfaceLayers", null, interfaceLayers);

			foreach (GameInterfaceLayer current in interfaceLayers)
			{
				if (!current.Draw())
				{
					break;
				}
			}

			PlayerInput.SetZoom_World();

			Main.graphics.GraphicsDevice.SetRenderTarget(null);
		}

		public static void DrawInterface(On.Terraria.Main.orig_DrawInterface orig, Main self, GameTime gameTime)
		{
			Main.spriteBatch.Begin();
			Main.spriteBatch.Draw(uiTarget, Vector2.Zero, Color.White);
			Main.spriteBatch.End();
		}
	}
}