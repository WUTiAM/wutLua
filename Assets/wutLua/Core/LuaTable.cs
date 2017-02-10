namespace wutLua
{
	using System;
	using System.Collections;

	public class LuaTable : LuaObjectBase
	{
		public object this[ string key ]
		{
			get { return _GetValue( key ); }
			set { _SetValue( key, value ); }
		}

		public object this[ int key ]
		{
			get { return _GetValue( key ); }
			set { _SetValue( key, value ); }
		}

		public LuaTable( LuaState luaState ) : base( luaState )
		{
			LuaLib.lua_newtable( luaState.L ); 											// |...|t
			_RefId = LuaLib.luaL_ref( luaState.L, LuaIndices.LUA_REGISTRYINDEX );		// |...|		// Registry[reference] = t
		}

		public LuaTable( LuaState luaState, int index ) : base( luaState )
		{
			if( !LuaLib.lua_isnil( luaState.L, index ) )
			{
				LuaLib.lua_pushvalue( luaState.L, index ); 								// |...|t|...|t
				_RefId = LuaLib.luaL_ref( luaState.L, LuaIndices.LUA_REGISTRYINDEX );	// |...|t|...|		// Registry[reference] = t
			}
		}

#region Object members
		public override string ToString()
		{
			return "table#" + _RefId.ToString();
		}
#endregion

		public object RawGet( string key )
		{
			IntPtr L = _LuaState.L;
			int oldTop = LuaLib.lua_gettop( L );

			Push();										// |t
			LuaLib.lua_pushstring( L, key ); 			// |t|k
			LuaLib.lua_rawget( L, -2 ); 				// |v
			object value = _LuaState.ToObject( -1 ); 	// |v

			LuaLib.lua_settop( L, oldTop ); 			// |

			return value;
		}

		public void RawSet( string key, object value )
		{
			IntPtr L = _LuaState.L;
			int oldTop = LuaLib.lua_gettop( L );

			Push(); 							// |t
			LuaLib.lua_pushstring( L, key );	// |t|k
			_LuaState.PushObject( value );		// |t|k|v
			LuaLib.lua_rawset( L, -3 );			// |t		// t.k = v

			LuaLib.lua_settop( L, oldTop ); 	// |
		}

		protected object _GetValue( string key )
		{
			IntPtr L = _LuaState.L;
			int oldTop = LuaLib.lua_gettop( L );

			Push();										// |t
			LuaLib.lua_pushstring( L, key ); 			// |t|k
			LuaLib.lua_gettable( L, -2 ); 				// |v
			object value = _LuaState.ToObject( -1 ); 	// |v

			LuaLib.lua_settop( L, oldTop ); 			// |

			return value;
		}

		protected void _SetValue( string key, object value )
		{
			IntPtr L = _LuaState.L;
			int oldTop = LuaLib.lua_gettop( L );

			Push(); 							// |t
			_LuaState.PushObject( value );		// |t|v
			LuaLib.lua_setfield( L, -2, key );	// |t		// t.k = v

			LuaLib.lua_settop( L, oldTop ); 	// |
		}

		protected object _GetValue( int key )
		{
			IntPtr L = _LuaState.L;
			int oldTop = LuaLib.lua_gettop( L );

			Push();										// |t
			LuaLib.lua_rawgeti( L, -1, key );			// |t|v
			object value = _LuaState.ToObject( -1 ); 	// |v

			LuaLib.lua_settop( L, oldTop ); 			// |

			return value;
		}

		protected void _SetValue( int key, object value )
		{
			IntPtr L = _LuaState.L;
			int oldTop = LuaLib.lua_gettop( L );

			Push(); 							// |t
			_LuaState.PushObject( value );		// |t|v
			LuaLib.lua_rawseti( L, -2, key );	// |t		// t[k] = v

			LuaLib.lua_settop( L, oldTop ); 	// |
		}
	}
}
