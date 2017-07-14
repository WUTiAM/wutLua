namespace wuanLua
{
	using System;
	using System.Collections.Generic;

	public enum LuaBindMetatableType
	{
		Type = 0,
		Instance,
	}

	public class LuaBindMetatable : LuaTable
	{
		const string _INDEX_META_METHOD_CODE = @"
local function __index( t, k )
	local mt = getmetatable( t )
	local o = nil

	-- Try to get member from cache
	local cmt = mt
	repeat
		o = cmt[k]
		if o ~= nil then
			if cmt ~= mt then
				mt[k] = o
			end

			break
		else
			if cmt.__base == nil then -- Haven't cache the base metatable
				cmt:__getbase() -- Retrive the base metatable, or False if there's no base type
			end

			cmt = cmt.__base
		end
	until not cmt

	if o == nil then
		-- Try to get memeber by reflection
		o = mt:__getmember( t, k )
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
		o = cmt[k]
		if o ~= nil then
			if cmt ~= mt then
				mt[k] = o
			end

			break
		else
			if cmt.__base == nil then -- Haven't cache the base metatable
				cmt:__getbase() -- Retrive the base metatable, or False if there's no base type
			end

			cmt = cmt.__base
		end
	until not cmt

	if o == nil then
		-- Try to get memeber by reflection
		o = mt:__getmember( t, k )
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
		LuaBindMetatableType _metatableType;

		public LuaBindMetatable( LuaState luaState, Type type, LuaBindMetatableType metatableType ) : base( luaState )
		{
			_luaState = luaState;
			_type = type;
			_metatableType = metatableType;

			IntPtr L = luaState.L;

			if( luaState.Bindings.MetatableIndexMetamethod == null )
			{
				LuaLib.luaL_dostring( L, _INDEX_META_METHOD_CODE );							// |f
				luaState.Bindings.MetatableIndexMetamethod = new LuaFunction( luaState, -1 );
				LuaLib.lua_pop( L, 1 );														// |
				LuaLib.luaL_dostring( L, _NEWINDEX_META_METHOD_CODE );						// |f
				luaState.Bindings.MetatableNewIndexMetamethod = new LuaFunction( luaState, -1 );
				LuaLib.lua_pop( L, 1 );														// |
			}
			if( _gcMetamethod == null )
			{
				_gcMetamethod = new LuaCSFunction( _GC );
				_getBaseMetatableMetamethod = new LuaCSFunction( _GetBaseMetatable );
				_getMemberMetamethod = new LuaCSFunction( _GetMember );
			}

			Push();															// |mt

			LuaLib.lua_pushstring( L, "__refId" );							// |mt|s
			LuaLib.lua_pushinteger( L, _RefId );							// |mt|s|v
			LuaLib.lua_rawset( L, -3 );										// |mt		// mt.__refId = _RefId

			LuaLib.lua_pushstring( L, "__index" );							// |mt|s
			luaState.Bindings.MetatableIndexMetamethod.Push();				// |mt|s|v
			LuaLib.lua_rawset( L, -3 );										// |mt		// mt.__index = f

			if( !type.IsEnum )
			{
				LuaLib.lua_pushstring( L, "__newindex" );					// |mt|s
				luaState.Bindings.MetatableNewIndexMetamethod.Push();		// |mt|s|v
				LuaLib.lua_rawset( L, -3 );									// |mt		// mt.__newindex = f
			}

			LuaLib.lua_pushstring( L, "__gc" );								// |mt|s
			LuaLib.lua_pushcsfunction( L, _gcMetamethod );					// |mt|s|csf
			LuaLib.lua_rawset( L, -3 );										// |mt		// mt.__gc = csf

			LuaLib.lua_pushstring( L, "__getbase" );						// |mt|s
			LuaLib.lua_pushcsfunction( L, _getBaseMetatableMetamethod );	// |mt|s|csf
			LuaLib.lua_rawset( L, -3 );										// |mt		// mt.__getbase = csf

			LuaLib.lua_pushstring( L, "__getmember" );						// |mt|s
			LuaLib.lua_pushcsfunction( L, _getMemberMetamethod );			// |mt|s|csf
			LuaLib.lua_rawset( L, -3 );										// |mt		// mt.__getmember = csf

			LuaLib.lua_pop( L, 1 );											// |

			luaState.Bindings.RegisterMetatableRefId( _RefId, this );
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

			int refId;
			LuaLib.lua_pushstring( L, "__refId" );			// |mt|s
			LuaLib.lua_rawget( L, 1 );						// |mt|v	// v = mt.s
			refId = (int) LuaLib.lua_tonumber( L, -1 );
			LuaLib.lua_pop( L, 1 );							// |mt

			LuaBindMetatable self = luaState.Bindings.GetMetatableByRefId( refId );
			Type baseType = self._type.BaseType;
			if( baseType == null )
			{
				// No base type
				LuaLib.lua_pushstring( L, "__base" );		// |mt|s
				LuaLib.lua_pushboolean( L, false );			// |mt|s|b
				LuaLib.lua_rawset( L, -3 );					// |mt		// mt.__base = false
			}
			else
			{
				LuaBindMetatable baseMetatable = luaState.Bindings.GetMetatable( baseType, self._metatableType );

				LuaLib.lua_pushstring( L, "__base" );		// |mt|s
				baseMetatable.Push();						// |mt|s|basemt
				LuaLib.lua_rawset( L, -3 );					// |mt		// mt.__base = basemt
			}

			return 0;
		}

		[MonoPInvokeCallback( typeof( LuaCSFunction ) )]
		static int _GetMember( IntPtr L )
		{
			LuaState luaState = LuaState.Get( L );

			string memberName = LuaLib.lua_tostring( L, -1 );
			object o = luaState.ToCSObject( -2 );

			// TODO: Reflection

			return 0;
		}

#region LuaObjectBase members
		public override void Dispose( bool disposeManagedResources )
		{
			_luaState.Bindings.UnregisterMetatableRefId( _RefId );

			base.Dispose( disposeManagedResources );
		}
#endregion
	}
};
