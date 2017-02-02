using System.Diagnostics;

namespace wutLua
{
	using System;
	using System.Collections.Generic;

	public class LuaFunction : LuaObjectBase
	{
		public LuaCSFunction Function { get; protected set; }

		public LuaFunction( LuaState luaState, int index ) : base( luaState )
		{
			LuaLib.lua_pushvalue( luaState.L, index );									// |...|f|...|f
			_RefId = LuaLib.luaL_ref( luaState.L, LuaIndices.LUA_REGISTRYINDEX );	// |...|f|...|		// Registry[reference] = f
		}

		#region Object members

		public override string ToString()
		{
			return "function#" + _RefId.ToString();
		}

		public override int GetHashCode()
		{
			return ( _RefId != 0 ? _RefId : Function.GetHashCode() );
		}

		#endregion

		public object[] Call( params object[] args )
		{
			IntPtr L = _LuaState.L;
			int oldTop = LuaLib.lua_gettop( L );

			LuaLib.lua_rawgeti( L, LuaIndices.LUA_REGISTRYINDEX, _RefId );		// |f

			if( args != null )
			{
				foreach( var arg in args )
				{
					_LuaState.PushObject( arg );									// |f|a1|a2|...
				}
			}

			if( LuaLib.lua_pcall( L, args.Length, LuaLib.LUA_MULTRET, 0 ) != 0 )	// | or |r1|r2|... or |e
			{
				string error = (string) _LuaState.ToObject( -1 );
				LuaLib.lua_settop( L, oldTop );										// |

				throw new LuaException( error );
			}

			object[] returnObjects = null;

			int currentTop = LuaLib.lua_gettop( L );
			if( currentTop != oldTop )
			{
				var objects = new List<object>();
				for( int i = oldTop + 1; i <= currentTop; ++i )
				{
					objects.Add( _LuaState.ToObject( i ) );
				}
				returnObjects = objects.ToArray();

				LuaLib.lua_settop( L, oldTop );										// |
			}

			return returnObjects;
		}
	}
}
