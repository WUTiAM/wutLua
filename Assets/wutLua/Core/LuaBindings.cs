namespace wuanLua
{
	using System;
	using System.Collections.Generic;

	// Marks a method, field or property to be accessable from Lua
	public sealed class LuaBindAttribute : Attribute
	{
		public LuaBindAttribute() {}
	}

	// Marks a method, field or property to be hidden from Lua
	public sealed class LuaBindIgnoreAttribute : Attribute
    {
		public LuaBindIgnoreAttribute() {}
    }

    public partial class LuaBindings
    {
	    public LuaFunction MetatableIndexMetamethod { get; set; }
	    public LuaFunction MetatableNewIndexMetamethod { get; set; }

	    LuaState _luaState;

	    Dictionary<string, Type> _typeNames = new Dictionary<string, Type>();
	    Dictionary<Type, LuaTable> _typeTables = new Dictionary<Type, LuaTable>();

	    Dictionary<Type, LuaBindMetatable>[] _metatables =
	    {
		    new Dictionary<Type, LuaBindMetatable>(), // CSObjectMetatableType.Type
		    new Dictionary<Type, LuaBindMetatable>(), // CSObjectMetatableType.Instance
	    };
	    Dictionary<int, LuaBindMetatable> _metatableRefIds = new Dictionary<int, LuaBindMetatable>(); // [refId]: metatable

	    public LuaBindings( LuaState luaState )
	    {
		    _luaState = luaState;
		}

	    public void Initialize()
	    {
		    _InitializeBindings();
	    }

		partial void _InitializeBindings();

	    public void RegisterType( Type type )
	    {
		    UnityEngine.Debug.Assert( !_typeTables.ContainsKey( type ) );

		    IntPtr L = _luaState.L;

		    LuaTable typeTable = new LuaTable( _luaState );
		    typeTable.Push();									// |t
		    LuaLib.lua_pushstring( L, "__typeName" );			// |t|sk
		    LuaLib.lua_pushstring( L, type.ToString() );		// |t|sk|sv
		    LuaLib.lua_rawset( L, -3 );							// |t		// t.__typeName = typeName

		    LuaBindMetatable metatable = GetMetatable( type, LuaBindMetatableType.Type );
		    metatable.Push();									// |t|mt
		    LuaLib.lua_setmetatable( L, -2 );					// |t		// t.metatable = mt

		    LuaLib.lua_pop( L, 1 );								// |

		    _typeNames.Add( type.ToString(), type );
		    _typeTables[type] = typeTable;
	    }

	    public bool ImportTypeToLua( string typeFullName )
	    {
		    Type type;
		    LuaTable typeTable;
		    if( !_typeNames.TryGetValue( typeFullName, out type ) || !_typeTables.TryGetValue( type, out typeTable ) )
		    {
			    // TODO: Reflection
			    typeTable = null;
		    }

		    typeTable.Push();	// |t

		    return true;
	    }

	    public bool GetRegisteredTypeByName( string typeName, out Type type )
	    {
		    return _typeNames.TryGetValue( typeName, out type );
	    }

	    public LuaBindMetatable GetMetatable( Type type, LuaBindMetatableType metatableType )
	    {
		    LuaBindMetatable metatable;

		    var metatables = _metatables[(int) metatableType];
		    if( !metatables.TryGetValue( type, out metatable ) )
		    {
			    metatable = new LuaBindMetatable( _luaState, type, metatableType );
			    metatables[type] = metatable;
		    }

		    return metatable;
	    }

	    public void RegisterMetatableRefId( int refId, LuaBindMetatable metatable )
	    {
		    _metatableRefIds.Add( refId, metatable );
	    }

	    public void UnregisterMetatableRefId( int refId )
	    {
		    _metatableRefIds.Remove( refId );
	    }

	    public LuaBindMetatable GetMetatableByRefId( int refId )
	    {
		    LuaBindMetatable metatable;
		    _metatableRefIds.TryGetValue( refId, out metatable );

		    return metatable;
	    }
    }
}
