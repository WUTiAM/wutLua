using System.Reflection;

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
		if o == nil then
			error( 'Cannot find member ' .. k .. '!' )
		end
	end

	if type( o ) == 'table' then
		-- Invoke the property member's getter
		if o[1] ~= nil then
			return o[1]( t )
		else
			error( 'The property ' .. k .. ' is write-only!' );
		end
	else
		return o
	end
end

return __index
		";
		const string _NEWINDEX_META_METHOD_CODE = @"
local function __newindex( t, k, v )
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
		if o == nil then
			error( 'Cannot find member ' .. k .. '!' )
		end
	end

	if type( o ) == 'table' then
		-- Invoke the property member's setter
		local setter = o[2]
		if setter ~= nil then
			return o[2]( t, v )
		else
			error( 'The property ' .. k .. ' is readonly!' );
		end
	else
		error( k .. ' is not a property member!' )
	end
end

return __newindex
		";

		static LuaCSFunction _gcMetamethod;
		static LuaCSFunction _getBaseMetatableMetamethod;
		static LuaCSFunction _getMemberMetamethod;

		LuaState _luaState;

		Type _type;
		bool _isTypeMetatable;
		LuaMetatable _baseMetatable;

		LuaTable _indexCacheTable;

		public LuaMetatable( LuaState luaState, Type type, bool isTypeMetatable ) : base( luaState )
		{
			_luaState = luaState;

			IntPtr L = luaState.L;

			if( luaState.MetatableIndexMetamethod == null )
			{
				LuaLib.luaL_dostring( L, _INDEX_META_METHOD_CODE );						// |f
				luaState.MetatableIndexMetamethod = new LuaFunction( luaState, -1 );	// |f
				LuaLib.lua_pop( L, 1 );													// |
				LuaLib.luaL_dostring( L, _NEWINDEX_META_METHOD_CODE );					// |f
				luaState.MetatableNewIndexMetamethod = new LuaFunction( luaState, -1 );	// |f
				LuaLib.lua_pop( L, 1 );													// |
			}
			if( _gcMetamethod == null )
			{
				_gcMetamethod = new LuaCSFunction( _GC );
				_getBaseMetatableMetamethod = new LuaCSFunction( _GetBaseMetatable );
				_getMemberMetamethod = new LuaCSFunction( _GetMember );
			}

			_type = type;
			_isTypeMetatable = isTypeMetatable;

			_indexCacheTable = new LuaTable( luaState );					// |

			Push();															// |mt

			LuaLib.lua_pushstring( L, "__refId" );							// |mt|k
			LuaLib.lua_pushinteger( L, _RefId );							// |mt|k|v
			LuaLib.lua_rawset( L, -3 );										// |mt		// mt.__refId = _RefId

			LuaLib.lua_pushstring( L, "__indexCache" );						// |mt|k
			_indexCacheTable.Push();										// |mt|k|v
			LuaLib.lua_rawset( L, -3 );										// |mt		// mt.__indexCache = t

			LuaLib.lua_pushstring( L, "__index" );							// |mt|k
			luaState.MetatableIndexMetamethod.Push();						// |mt|k|v
			LuaLib.lua_rawset( L, -3 );										// |mt		// mt.__index = f

			LuaLib.lua_pushstring( L, "__newindex" );						// |mt|k
			luaState.MetatableNewIndexMetamethod.Push();					// |mt|k|v
			LuaLib.lua_rawset( L, -3 );										// |mt		// mt.__newindex = f

			LuaLib.lua_pushstring( L, "__gc" );								// |mt|k
			LuaLib.lua_pushcsfunction( L, _gcMetamethod );					// |mt|k|v
			LuaLib.lua_rawset( L, -3 );										// |mt		// mt.__index = csf

			LuaLib.lua_pushstring( L, "getBaseMetatable" );					// |mt|k
			LuaLib.lua_pushcsfunction( L, _getBaseMetatableMetamethod );	// |mt|k|v
			LuaLib.lua_rawset( L, -3 );										// |mt		// mt.getBaseMetatable = csf

			LuaLib.lua_pushstring( L, "getMember" );						// |mt|k
			LuaLib.lua_pushcsfunction( L, _getMemberMetamethod );			// |mt|k|v
			LuaLib.lua_rawset( L, -3 );										// |mt		// mt.getMember = csf

			LuaLib.lua_pop( L, 1 );											// |

			luaState.Metatables.Add( _RefId, this );
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

			LuaLib.lua_pushstring( L, "__refId" );			// |mt|k
			LuaLib.lua_rawget( L, 1 );						// |mt|v
			int refId = (int) LuaLib.lua_tonumber( L, -1 );
			LuaLib.lua_pop( L, 1 );							// |mt

			LuaMetatable self;
			luaState.Metatables.TryGetValue( refId, out self );
			if( self._baseMetatable == null )
			{
				Type baseType = self._type.BaseType;
				if( baseType == null || baseType == typeof( Object ) )
				{
					// No base type
					LuaLib.lua_pushstring( L, "__base" );	// |mt|k
					LuaLib.lua_pushboolean( L, false );		// |mt|k|v
					LuaLib.lua_rawset( L, -3 );				// |mt		// mt.__base = false

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

			LuaLib.lua_pushstring( L, "__base" );	// |mt|k
			self._baseMetatable.Push();				// |mt|k|v
			LuaLib.lua_rawset( L, -3 );				// |mt		// mt.__base = basemt

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
			object o = luaState.ToCSObject( -2 );

			// TODO: Reflection
			LuaLib.lua_pushstring( L, memberName + ( o as UnityEngine.Object ).name );
			LuaLib.lua_pushboolean( L, false );

			return 2;
		}

		public void AddMember( string name, LuaCSFunction getter, LuaCSFunction setter )
		{
			IntPtr L = _LuaState.L;

			_indexCacheTable.Push();							// |t
			LuaLib.lua_pushstring( L, name );					// |t|k
			LuaLib.lua_newtable( L );							// |t|k|nt
			if( getter != null )
			{
				LuaLib.lua_pushcsfunction( L, getter );			// |t|k|nt|csf
			}
			else
			{
				LuaLib.lua_pushnil( L );						// |t|k|nt|nil
			}
			LuaLib.lua_rawseti( L, -2, 1 );						// |t|k|nt		// nt[1] = getter
			if( setter != null )
			{
				LuaLib.lua_pushcsfunction( L, setter );			// |t|k|nt|csf
			}
			else
			{
				LuaLib.lua_pushnil( L );						// |t|k|nt|nil
			}
			LuaLib.lua_rawseti( L, -2, 2 );						// |t|k|nt		// nt[2] = setter
			LuaLib.lua_rawset( L, -3 );							// |t			// t[k] = nt, t == this.__indexCache, k == name

			LuaLib.lua_pop( L, 1 );								// |
		}

		public void AddMember( string name, LuaCSFunction function )
		{
			IntPtr L = _LuaState.L;

			_indexCacheTable.Push();					// |t

			LuaLib.lua_pushstring( L, name );			// |t|k
			LuaLib.lua_pushcsfunction( L, function );	// |t|k|v
			LuaLib.lua_rawset( L, -3 );					// |t		// t.k = v

			LuaLib.lua_pop( L, 1 );						// |
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
