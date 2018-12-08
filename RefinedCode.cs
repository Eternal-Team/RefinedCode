using Terraria.ModLoader;

namespace RefinedCode
{
	public class RefinedCode : Mod
	{
		public override void Load()
		{
			Hooking.Hooking.Initialize();
		}
	}
}