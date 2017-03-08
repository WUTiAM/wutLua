namespace wutLuaBind
{
	using System;
	using wutLua;

	public class LuaBindBase
	{
		static LuaMetatable _currentMetatable;

		protected static void _StartToRegisterTypeMembers( LuaState luaState, Type type, LuaCSFunction constructor )
		{
			luaState.RegisterType( type, constructor );
			_currentMetatable = luaState.GetTypeMetatable( type );
		}

		protected static void _StartToRegisterObjectMembers( LuaState luaState, Type type )
		{
			_currentMetatable = luaState.GetObjectMetatable( type );
		}

		protected static void _RegisterMember( string name, LuaCSFunction getter, LuaCSFunction setter )
		{
			_currentMetatable.AddMember( name, getter, setter );
		}

		protected static void _RegisterMember( string name, LuaCSFunction function )
		{
			_currentMetatable.AddMember( name, function );
		}

		protected static bool _CheckLuaType( IntPtr L, int index, params LuaTypes[] luaTypes )
		{
			LuaTypes luaType = LuaLib.lua_type( L, 2 );

			for( int i = 0; i < luaTypes.Length; ++i )
			{
				if( luaType == luaTypes[i] )
					return true;
			}

			return false;
		}
	}
}
