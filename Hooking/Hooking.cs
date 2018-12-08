using On.Terraria;

namespace RefinedCode.Hooking
{
	public static partial class Hooking
	{
		public static void Initialize()
		{
			Main.do_Draw += (orig, self, time) => orig(self, time);
			Main.DrawInterface += (orig, self, time) => orig(self, time);
			Main.EnsureRenderTargetContent += (orig, self) => orig(self);
		}
	}
}