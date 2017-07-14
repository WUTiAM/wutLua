namespace wuanLuaBind
{
	using System;
	using wuanLua;

	public class LuaBindBase
	{
		public static LuaState LuaState { get; set; }

		protected static void _BeginTypeMembers( Type type, LuaCSFunction constructor )
		{
			IntPtr L = LuaState.L;

			LuaBindMetatable typeMetatable = LuaState.Bindings.GetMetatable( type, LuaBindMetatableType.Type );
			typeMetatable.Push();								// |mt

			if( constructor != null )
			{
				LuaLib.lua_pushstring( L, "__call" );			// |mt|s
				LuaLib.lua_pushcsfunction( L, constructor );	// |mt|s|csf
				LuaLib.lua_rawset( L, -3 );						// |mt			// mt.__call = constructor
			}
		}

		protected static void _BeginInstanceMembers( Type type )
		{
			LuaBindMetatable instanceMetatable = LuaState.Bindings.GetMetatable( type, LuaBindMetatableType.Instance );
			instanceMetatable.Push();							// |mt
		}

		protected static void _AddMember( string name, LuaCSFunction getter, LuaCSFunction setter )
		{
			IntPtr L = LuaState.L;

			LuaLib.lua_pushstring( L, name );					// |mt|s
			LuaLib.lua_newtable( L );							// |mt|s|nt

			if( getter != null )
			{
				LuaLib.lua_pushcsfunction( L, getter );			// |mt|s|nt|csf
			}
			else
			{
				LuaLib.lua_pushnil( L );						// |mt|s|nt|nil
			}
			LuaLib.lua_rawseti( L, -2, 1 );						// |mt|s|nt		// nt[1] = getter, getter = csf or nil

			if( setter != null )
			{
				LuaLib.lua_pushcsfunction( L, setter );			// |mt|s|nt|csf
			}
			else
			{
				LuaLib.lua_pushnil( L );						// |mt|s|nt|nil
			}
			LuaLib.lua_rawseti( L, -2, 2 );						// |mt|s|nt		// nt[2] = setter, setter = csf or nil

			LuaLib.lua_rawset( L, -3 );							// |mt			// mt.s = nt, s == name
		}

		protected static void _AddMember( string name, LuaCSFunction function )
		{
			IntPtr L = LuaState.L;

			LuaLib.lua_pushstring( L, name );					// |mt|s
			LuaLib.lua_pushcsfunction( L, function );			// |mt|s|csf
			LuaLib.lua_rawset( L, -3 );							// |mt			// t.s = csf, s = name
		}

		protected static void _AddMember( string name, int value )
		{
			IntPtr L = LuaState.L;

			LuaLib.lua_pushstring( L, name );					// |mt|s
			LuaLib.lua_pushinteger( L, value );					// |mt|s|v
			LuaLib.lua_rawset( L, -3 );							// |mt			// t.s = v
		}

		protected static void _RegisterType( Type type )
		{
			LuaState.Bindings.RegisterType( type );
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
