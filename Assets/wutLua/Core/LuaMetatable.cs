namespace wutLua
{
	using System;
	using System.Collections.Generic;

	public class LuaMetatable : LuaTable
	{
		const string _INDEX_META_METHOD_CODE = @"
local function __index( t, k )
	local mt = getmetatable( t )
	local o = nil

	-- Try to get member from cache
	local cmt = mt
	repeat
		o = cmt.__indexCache[k]
		if o ~= nil then
			if cmt ~= mt then
				mt.__indexCache[k] = o
			end

			break
		else
			if cmt.__base == nil then -- Haven't cache the base metatable
				cmt:getBaseMetatable() -- Retrive the base metatable, or False if there's no base type
			end

			cmt = cmt.__base
		end
	until not cmt

	if o == nil then
		-- Try to get memeber by reflection
		o = mt:getMember( t, k )
	end

	if type( o ) == 'table' then
		return o[1]( t )
	else
		return o
	end
end

return __index
		";

		static LuaCSFunction _newIndexMetamethod;
		static LuaCSFunction _gcMetamethod;
		static LuaCSFunction _getBaseMetatableMetamethod;
		static LuaCSFunction _getMemberMetamethod;

		LuaState _luaState;

		Type _type;
		bool _isTypeMetatable;
		LuaMetatable _baseMetatable;

		LuaTable _indexCacheTable;

		Dictionary<string, object> _memberCache = new Dictionary<string, object>();

		public LuaMetatable( LuaState luaState, Type type, bool isTypeMetatable ) : base( luaState )
		{
			_luaState = luaState;

			IntPtr L = luaState.L;

			if( luaState.MetatableIndexMetamethod == null )
			{
				LuaLib.luaL_dostring( L, _INDEX_META_METHOD_CODE );						// |f
				luaState.MetatableIndexMetamethod = new LuaFunction( luaState, -1 );	// |f
				LuaLib.lua_pop( L, 1 );													// |
			}
			if( _newIndexMetamethod == null )
			{
				_newIndexMetamethod = new LuaCSFunction( _NewIndex );
				_gcMetamethod = new LuaCSFunction( _GC );
				_getBaseMetatableMetamethod = new LuaCSFunction( _GetBaseMetatable );
				_getMemberMetamethod = new LuaCSFunction( _GetMember );
			}

			_type = type;
			_isTypeMetatable = isTypeMetatable;

			_indexCacheTable = new LuaTable( luaState );				// |

			RawSet( "__refId", _RefId );								// |		// mt.__refId = _RefId
			RawSet( "__indexCache", _indexCacheTable );					// |		// mt.__indexCache = t
			RawSet( "__index", luaState.MetatableIndexMetamethod );		// |		// mt.__index = f
			RawSet( "__newindex", _newIndexMetamethod );				// |		// mt.__newindex = csf
			RawSet( "__gc", _gcMetamethod );							// |		// mt.__index = csf
			RawSet( "getBaseMetatable", _getBaseMetatableMetamethod );	// |		// mt.getBaseMetatable = csf
			RawSet( "getMember", _getMemberMetamethod );				// |		// mt.getMember = csf

			luaState.Metatables.Add( _RefId, this );
		}

		[MonoPInvokeCallback( typeof( LuaCSFunction ) )]
		static int _NewIndex( IntPtr L )
		{
			// TODO

			return 0;
		}

		[MonoPInvokeCallback( typeof( LuaCSFunction ) )]
		static int _GC( IntPtr L )
		{
			LuaState luaState = LuaState.Get( L );

			luaState.GCCSObject( 1 );

			return 0;
		}

		[MonoPInvokeCallback( typeof( LuaCSFunction ) )]
		static int _GetBaseMetatable( IntPtr L )
		{
			LuaState luaState = LuaState.Get( L );

			LuaMetatable self = _GetSelf( L );
			if( self._baseMetatable == null )
			{
				Type baseType = self._type.BaseType;
				if( baseType == null || baseType == typeof( Object ) )
				{
					// No base type
					self.RawSet( "__base", false );			// |		// __base = false

					return 0;
				}

				if( self._isTypeMetatable )
				{
					self._baseMetatable = luaState.GetTypeMetatable( baseType );
					if( self._baseMetatable == null )
					{
						luaState.RegisterType( baseType, _DefaultConstructor );
						self._baseMetatable = luaState.GetTypeMetatable( baseType );
					}
				}
				else
				{
					self._baseMetatable = luaState.GetObjectMetatable( baseType );
				}
			}

			self.RawSet( "__base", self._baseMetatable );	// |		// __base = basemt

			return 0;
		}

		[MonoPInvokeCallback( typeof( LuaCSFunction ) )]
		static int _DefaultConstructor( IntPtr L )
		{
			return 0;
		}

		[MonoPInvokeCallback( typeof( LuaCSFunction ) )]
		static int _GetMember( IntPtr L )
		{
			LuaState luaState = LuaState.Get( L );

			string memberName = LuaLib.lua_tostring( L, -1 );
			object o = luaState.ToObject( -2 );

			// TODO: Reflection
			LuaLib.lua_pushstring( L, memberName + ( o as UnityEngine.Object ).name );
			LuaLib.lua_pushboolean( L, false );

			return 2;
		}

		static LuaMetatable _GetSelf( IntPtr L )
		{
			LuaState luaState = LuaState.Get( L );

			LuaLib.lua_pushstring( L, "__refId" );	// |mt|k
			LuaLib.lua_rawget( L, -2 );				// |mt|v
			int refId = (int) LuaLib.lua_tonumber( L, -1 );
			LuaLib.lua_pop( L, 1 );					// |mt

			LuaMetatable self;
			luaState.Metatables.TryGetValue( refId, out self );

			return self;
		}

		public void AddMember( string name, LuaCSFunction getter, LuaCSFunction setter )
		{
			IntPtr L = _LuaState.L;
			int oldTop = LuaLib.lua_gettop( L );

			_indexCacheTable.Push();							// |t
			LuaLib.lua_pushstring( L, name );					// |t|k
			LuaLib.lua_newtable( L );							// |t|k|nt
			LuaLib.lua_pushcsfunction( L, getter );				// |t|k|nt|csf
			LuaLib.lua_rawseti( L, -2, 1 );						// |t|k|nt		// nt[1] = csf
			LuaLib.lua_rawset( L, -3 );							// |t			// t[k] = nt, t == this.__indexCache, k == name

			LuaLib.lua_settop( L, oldTop );						// |
		}

		public void AddMember( string name, LuaCSFunction function )
		{
			_indexCacheTable.RawSet( name, function );
		}

#region LuaObjectBase members
		public override void Dispose( bool disposeManagedResources )
		{
			_luaState.Metatables.Remove( _RefId );

			base.Dispose( disposeManagedResources );
		}
#endregion
	}
};
