namespace wuanLua
{
	using System;

	public static class Config
	{
		public static string LuaBindGeneratedCodePath = "wutLua/Generated";

		public static Type[] LuaBindList = new Type[]
		{
			typeof( System.Object ),
			typeof( System.Array ),

			typeof( UnityEngine.Animation ),
			typeof( UnityEngine.AnimationClip ),
			typeof( UnityEngine.AnimationState ),
			typeof( UnityEngine.AudioSource ),
			typeof( UnityEngine.AudioClip ),
			typeof( UnityEngine.Camera ),
			typeof( UnityEngine.Component ),
			typeof( UnityEngine.GameObject ),
			typeof( UnityEngine.Light ),
			typeof( UnityEngine.Object ),
			typeof( UnityEngine.Material ),
			typeof( UnityEngine.Space ),
			typeof( UnityEngine.Time ),
			typeof( UnityEngine.Transform ),
		};
	}
}
