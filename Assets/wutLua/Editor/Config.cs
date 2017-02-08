namespace wutLua
{
	using System;

	public static class Config
	{
		public static string LuaBindGeneratedCodePath = "wutLua/Generated";

		public static Type[] LuaBindList = new Type[]
		{
			typeof( System.Object ),

			typeof( UnityEngine.AudioSource ),
			typeof( UnityEngine.Camera ),
			typeof( UnityEngine.Component ),
			typeof( UnityEngine.GameObject ),
			typeof( UnityEngine.Light ),
			typeof( UnityEngine.Object ),
			typeof( UnityEngine.Material ),
			typeof( UnityEngine.Time ),
			typeof( UnityEngine.Transform ),
		};
	}
}
