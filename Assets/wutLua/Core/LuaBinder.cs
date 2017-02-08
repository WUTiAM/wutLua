namespace wutLua
{
    using System;

	public sealed class LuaBindAttribute : Attribute
	{
		public LuaBindAttribute()
		{
		}
	}

	public sealed class LuaBindIgnoreAttribute : Attribute
    {
		public LuaBindIgnoreAttribute()
		{
		}
    }

    public partial class LuaBinder
    {
		public static void Initialize( LuaState luaState )
		{
			_Initialize( luaState );
		}

		static partial void _Initialize( LuaState luaState );
    }
}
