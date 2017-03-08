using UnityEngine;

namespace wutLua
{
	using System;
	using System.Collections.Generic;

	public enum LuaValueType
	{
		None = 0,

		Color = 1,
		Quaternion = 2,
		Vector2 = 3,
		Vector3 = 4,
		Vector4 = 5,
	}

	public class LuaState : IDisposable
	{
 		public IntPtr L { get; private set; }

		public LuaFunction MetatableIndexMetamethod;
		public LuaFunction MetatableNewIndexMetamethod;
		public Dictionary<int, LuaMetatable> Metatables = new Dictionary<int, LuaMetatable>();

		static Dictionary<IntPtr, LuaState> _luaStates = new Dictionary<IntPtr, LuaState>();

		Dictionary<string, Type> _typeNames = new Dictionary<string, Type>();
		Dictionary<Type, LuaTable> _typeTables = new Dictionary<Type, LuaTable>();
		Dictionary<Type, LuaMetatable> _typeMetatables = new Dictionary<Type, LuaMetatable>();
		Dictionary<Type, LuaMetatable> _objectMetatables = new Dictionary<Type, LuaMetatable>();

		int _newObjectUserdataValue = 1;
		Dictionary<int, object> _objects = new Dictionary<int, object>();
		Dictionary<object, int> _objectUserdataRefIds = new Dictionary<object, int>();

		public LuaState()
		{
			L = LuaLib.luaL_newstate();
			if( L == IntPtr.Zero )
			{
				UnityEngine.Debug.LogError( "Failed to create Lua state!" );
				return;
			}

			_luaStates[L] = this;

			LuaLib.luaL_openlibs( L );

			LuaCSFunction panicCallback = new LuaCSFunction( _PanicCallback );
			LuaLib.lua_atpanic( L, panicCallback );

			Set( "print", (LuaCSFunction) _Print );

			LuaLib.lua_newtable( L );						// |t
			LuaLib.lua_pushcsfunction( L, _ImportType );	// |t|csf
			LuaLib.lua_setfield( L, -2, "ImportType" );		// |t
			LuaLib.lua_setglobal( L, "wutLua" );			// |		// _G["wutLua"] = t

			LuaBinder.Initialize( this );
		}

		[MonoPInvokeCallback( typeof( LuaCSFunction ) )]
		static int _PanicCallback( IntPtr L )
		{
			string reason = string.Format(
				"Unprotected error in call to Lua API ({0})",
				LuaLib.lua_tostring( L, -1 ) );
			throw new LuaException( reason );
		}

		[MonoPInvokeCallback( typeof( LuaCSFunction ) )]
		static int _Print( IntPtr L )
		{
			string message = LuaLib.lua_tostring( L, -1 );
			UnityEngine.Debug.Log( "[Lua] " + message );

			return 0;
		}

		[MonoPInvokeCallback( typeof( LuaCSFunction ) )]
		int _ImportType( IntPtr L )
		{
			string typeFullName = LuaLib.lua_tostring( L, -1 );
			Type type;
			LuaTable typeTable;
			if( !_typeNames.TryGetValue( typeFullName, out type ) || !_typeTables.TryGetValue( type, out typeTable ) )
			{
				// TODO: Reflection
				typeTable = null;
			}

			typeTable.Push();	// |t

			return 1;
		}

		// -------------------------------------------------------------------------------------------------------------
		// Public interfaces
		// -------------------------------------------------------------------------------------------------------------

		public object[] DoBuffer( byte[] chunk, string chunkName = "chunk" )
		{
			int oldTop = LuaLib.lua_gettop( L );

			if( LuaLib.luaL_loadbuffer( L, chunk, chunk.Length, chunkName ) != 0 )	// |f or |err
			{
				string error = LuaLib.lua_tostring( L, -1 );						// |err
				LuaLib.lua_settop( L, oldTop );										// |

				throw new LuaException( error );
			}
			if( LuaLib.lua_pcall( L, 0, LuaLib.LUA_MULTRET, 0 ) != 0 )				// | or |ret1|ret2 or |err
			{
				string error = LuaLib.lua_tostring( L, -1 );						// |err
				LuaLib.lua_settop( L, oldTop );										// |

				throw new LuaException( error );
			}

			int top = LuaLib.lua_gettop( L );
			if( top == oldTop )
				return null;

			var objects = new List<object>();

			for( int i = oldTop + 1; i <= top; ++i )
			{
				objects.Add( ToObject( i ) );
			}

			LuaLib.lua_pop( L, top - oldTop );

			return objects.ToArray();
		}

		public object Get( string path )
		{
			int oldTop = LuaLib.lua_gettop( L );

			LuaLib.lua_pushvalue( L, LuaIndices.LUA_GLOBALSINDEX );	// |_G

			int startPos = 0;
			int pos;
			do
			{
				pos = path.IndexOf( '.', startPos );
				if( pos < 0 )
				{
					pos = path.Length;
				}

				string key = path.Substring( startPos, pos - startPos );

				LuaLib.lua_pushstring( L, key ); 					// |_G|v1|...|vn-1|kn
				LuaLib.lua_rawget( L, -2 ); 						// |_G|v1|...|vn-1|vn

				startPos = pos + 1;
			} while( startPos < path.Length && !LuaLib.lua_isnil( L, -1 ) );

			object o = ToObject( -1 );

			LuaLib.lua_settop( L, oldTop );							// |

			return o;
		}

		public void Set( string path, object value )
		{
			int oldTop = LuaLib.lua_gettop( L );

			LuaLib.lua_pushvalue( L, LuaIndices.LUA_GLOBALSINDEX );	// |_G

			int startPos = 0;
			int pos = path.IndexOf( '.' );
			while( pos >= 0 )
			{
				string key = path.Substring( startPos, pos - startPos );

				LuaLib.lua_pushstring( L, key ); 					// |_G|k1 or |_G|v1|k2 or |_G|v1|v2|k3 or ...
				LuaLib.lua_rawget( L, -2 ); 						// |_G|v1 or |_G|v1|v2 or |_G|v1|v2|v3 or ...

				if( LuaLib.lua_isnil( L, -1 ) )
				{
					startPos = path.Length;
					break;
				}

				startPos = pos + 1;
				pos = path.IndexOf( '.', startPos );
			}

			if( startPos < path.Length )
			{
				string key = path.Substring( startPos, path.Length - startPos );

				PushObject( value );								// |_G|v1|v2|...|vn-1|o
				LuaLib.lua_setfield( L, -2, key );					// |_G|v1|v2|...|vn-1|		// vn-1.kn = o
			}

			LuaLib.lua_settop( L, oldTop );							// |
		}

		// -------------------------------------------------------------------------------------------------------------
		// Internal use only
		// -------------------------------------------------------------------------------------------------------------

		public static LuaState Get( IntPtr L )
		{
			return _luaStates[L];
		}

		public void RegisterType( Type type, LuaCSFunction constructor )
		{
#if UNITY_EDITOR
			UnityEngine.Debug.Assert( !_typeMetatables.ContainsKey( type ) );
#endif

			LuaTable typeTable = new LuaTable( this );
			LuaMetatable typeMetatable = new LuaMetatable( this, type, true );

			typeTable.Push();									// |t
			LuaLib.lua_pushstring( L, "__typeName" );			// |t|sk
			LuaLib.lua_pushstring( L, type.ToString() );		// |t|sk|sv
			LuaLib.lua_rawset( L, -3 );							// |t		// t.__typeName = typeName

			typeMetatable.Push();								// |t|mt
			if( constructor != null )
			{
				LuaLib.lua_pushstring( L, "__call" );			// |t|mt|s
				LuaLib.lua_pushcsfunction( L, constructor );	// |t|mt|s|csf
				LuaLib.lua_rawset( L, -3 );						// |t|mt		// mt.__call = constructor
			}
			LuaLib.lua_setmetatable( L, -2 );					// |t			// t.metatable = mt

			LuaLib.lua_pop( L, 1 );								// |

			_typeNames.Add( type.ToString(), type );
			_typeTables.Add( type, typeTable );
			_typeMetatables.Add( type, typeMetatable );
		}

		public LuaMetatable GetTypeMetatable( Type type )
		{
			LuaMetatable metatable;
			_typeMetatables.TryGetValue( type, out metatable );

			return metatable;
		}

		public LuaMetatable GetObjectMetatable( Type type )
		{
			LuaMetatable metatable;
			if( !_objectMetatables.TryGetValue( type, out metatable ) )
			{
				metatable = new LuaMetatable( this, type, false );

				_objectMetatables.Add( type, metatable );
			}

			return metatable;
		}

		public bool CheckType( int index, Type type )
		{
			if( type == typeof( object ) )
				return true;

			LuaTypes luaType = LuaLib.lua_type( L, index );
			switch( luaType )
			{
				case LuaTypes.LUA_TBOOLEAN:
					return type == typeof( bool );
				case LuaTypes.LUA_TNUMBER:
					return type.IsPrimitive;
				case LuaTypes.LUA_TSTRING:
					return type == typeof( string ) || type == typeof( char[] ) || type == typeof( byte[] );
				case LuaTypes.LUA_TTABLE:
				{
					if( type.IsArray )
					{
						// TODO
						return false;
					}
					if( type == typeof( Type ) )
					{
						LuaLib.lua_pushstring( L, "__typeName" );	// |k
						LuaLib.lua_rawget( L, index );				// |v
						bool ret = !LuaLib.lua_isnil( L, -1 );
						LuaLib.lua_pop( L, 1 );						// |

						return ret;
					}

					return false;
				}
				case LuaTypes.LUA_TFUNCTION:
					return false;
				case LuaTypes.LUA_TUSERDATA:
					return false;
			}

			return false;
		}

		public bool CheckArray( int index, int count, Type type )
		{
			for( int i = 0; i < count; ++i )
			{
				if( !CheckType( index + i, type ) )
				{
					return false;
				}
			}

			return true;
		}







		public object ToObject( int index )
		{
			LuaTypes luaType = LuaLib.lua_type( L, index );
			switch( luaType )
			{
				case LuaTypes.LUA_TNONE:
				case LuaTypes.LUA_TNIL:
				{
					return null;
				}
				case LuaTypes.LUA_TBOOLEAN:
				{
					return LuaLib.lua_toboolean( L, index );
				}
				case LuaTypes.LUA_TNUMBER:
				{
					return LuaLib.lua_tonumber( L, index );
				}
				case LuaTypes.LUA_TSTRING:
				{
					return LuaLib.lua_tostring( L, index );
				}
				case LuaTypes.LUA_TTABLE:
				{
					LuaValueType valueType = LuaValueType.None;

					LuaLib.lua_pushvalue( L, index ); 								// |t
					LuaLib.lua_pushstring( L, "__valueType" ); 						// |t|k
					LuaLib.lua_rawget( L, -2 );										// |t|vt
					if( !LuaLib.lua_isnil( L, -1 ) )
					{
						valueType = (LuaValueType) LuaLib.lua_tonumber( L, -1 );	// |t|vt
					}
					LuaLib.lua_pop( L, 2 ); 										// |

					switch( valueType )
					{
						case LuaValueType.None:
						{
							return ToTable( index );
						}
						case LuaValueType.Color:
						{
							return ToColor( index );
						}
						case LuaValueType.Quaternion:
						{
							return ToQuaternion( index );
						}
						case LuaValueType.Vector2:
						{
							return ToVector2( index );
						}
						case LuaValueType.Vector3:
						{
							return ToVector3( index );
						}
						case LuaValueType.Vector4:
						{
							return ToVector4( index );
						}
						default:
						{
							return null;
						}
					}
				}
				case LuaTypes.LUA_TFUNCTION:
				{
					return ToFunction( index );
				}
				case LuaTypes.LUA_TUSERDATA:
				{
					return ToCSObject( index );
				}
				default:
				{
					return null;
				}
			}
		}

		public object ToObject( int index, Type type )
		{
			if( type == typeof( Type ) )
			{
				Type o;
				ToType( index, out o );

				return o;
			}
			if( type == typeof( bool ) )
			{
				return LuaLib.lua_toboolean( L, index );
			}
			if( type.IsPrimitive )
			{
				return LuaLib.lua_tonumber( L, index );
			}
			if( type == typeof( string ) )
			{
				return LuaLib.lua_tostring( L, index );
			}

			return ToObject( index );
		}

		public bool ToType( int index, out Type o )
		{
			o = null;

			LuaTypes luaType = LuaLib.lua_type( L, index );
			if( luaType == LuaTypes.LUA_TTABLE )
			{
				LuaLib.lua_pushstring( L, "__typeName" );	// |k
				LuaLib.lua_rawget( L, index );				// |v
				if( LuaLib.lua_isstring( L, -1 ) )
				{
					return _typeNames.TryGetValue( LuaLib.lua_tostring( L, -1 ), out o );
				}
			}

			return true;
		}

		public void ToEnum<T>( int index, out T o )
		{
			o = default( T );
		}

		public void ToArray<T>( int index, int count, out T[] o )
		{
			o = new T[count];
			for( int i = 0; i < count; ++i )
			{
				o[i] = (T) ToObject( index + i, typeof( T ) );
			}
		}

		public void ToArray( int index, int count, out bool[] o )
		{
			o = new bool[count];
			for( int i = 0; i < count; ++i )
			{
				o[i] = LuaLib.lua_toboolean( L, index + i );
			}
		}

		public void ToArray( int index, int count, out double[] o )
		{
			o = new double[count];
			for( int i = 0; i < count; ++i )
			{
				o[i] = LuaLib.lua_tonumber( L, index + i );
			}
		}

		public void ToArray( int index, int count, out float[] o )
		{
			o = new float[count];
			for( int i = 0; i < count; ++i )
			{
				o[i] = (float) LuaLib.lua_tonumber( L, index + i );
			}
		}

		public void ToArray( int index, int count, out int[] o )
		{
			o = new int[count];
			for( int i = 0; i < count; ++i )
			{
				o[i] = (int) LuaLib.lua_tonumber( L, index + i );
			}
		}

		public void ToArray( int index, int count, out string[] o )
		{
			o = new string[count];
			for( int i = 0; i < count; ++i )
			{
				o[i] = LuaLib.lua_tostring( L, index + i );
			}
		}












		public LuaTable ToTable( int index )
		{
			return new LuaTable( this, index );
		}

		public UnityEngine.Color ToColor( int index )
		{
			UnityEngine.Color color = new UnityEngine.Color( 0f, 1f, 1f, 1f );

			if( LuaLib.lua_type( L, index ) == LuaTypes.LUA_TTABLE )
			{
				LuaLib.lua_rawgeti( L, index, 1 );					// |r
				color.r = (float) LuaLib.lua_tonumber( L, -1 );		// |r
				LuaLib.lua_rawgeti( L, index, 2 );					// |r|g
				color.g = (float) LuaLib.lua_tonumber( L, -1 );		// |r|g
				LuaLib.lua_rawgeti( L, index, 3 );					// |r|g|b
				color.b = (float) LuaLib.lua_tonumber( L, -1 );		// |r|g|b
				LuaLib.lua_rawgeti( L, index, 4 );					// |r|g|b|a
				color.a = (float) LuaLib.lua_tonumber( L, -1 );		// |r|g|b|a
				LuaLib.lua_pop( L, 4 );								// |
			}

			return color;
		}

		public UnityEngine.Quaternion ToQuaternion( int index )
		{
			UnityEngine.Quaternion quaternion = new UnityEngine.Quaternion();

			if( LuaLib.lua_type( L, index ) == LuaTypes.LUA_TTABLE )
			{
				LuaLib.lua_rawgeti( L, index, 1 );						// |x
				quaternion.x = (float) LuaLib.lua_tonumber( L, -1 );	// |x
				LuaLib.lua_rawgeti( L, index, 2 );						// |x|y
				quaternion.y = (float) LuaLib.lua_tonumber( L, -1 );	// |x|y
				LuaLib.lua_rawgeti( L, index, 3 );						// |x|y|z
				quaternion.z = (float) LuaLib.lua_tonumber( L, -1 );	// |x|y|z
				LuaLib.lua_rawgeti( L, index, 4 );						// |x|y|z|w
				quaternion.w = (float) LuaLib.lua_tonumber( L, -1 );	// |x|y|z|w
				LuaLib.lua_pop( L, 4 );									// |
			}

			return quaternion;
		}

		public UnityEngine.Vector2 ToVector2( int index )
		{
			UnityEngine.Vector2 vector = new UnityEngine.Vector2();

			if( LuaLib.lua_type( L, index ) == LuaTypes.LUA_TTABLE )
			{
				LuaLib.lua_rawgeti( L, index, 1 );					// |x
				vector.x = (float) LuaLib.lua_tonumber( L, -1 );	// |x
				LuaLib.lua_rawgeti( L, index, 2 );					// |x|y
				vector.y = (float) LuaLib.lua_tonumber( L, -1 );	// |x|y
				LuaLib.lua_pop( L, 2 );								// |
			}

			return vector;
		}

		public UnityEngine.Vector3 ToVector3( int index )
		{
			UnityEngine.Vector3 vector = new UnityEngine.Vector3();

			if( LuaLib.lua_type( L, index ) == LuaTypes.LUA_TTABLE )
			{
				LuaLib.lua_rawgeti( L, index, 1 );					// |x
				vector.x = (float) LuaLib.lua_tonumber( L, -1 );	// |x
				LuaLib.lua_rawgeti( L, index, 2 );					// |x|y
				vector.y = (float) LuaLib.lua_tonumber( L, -1 );	// |x|y
				LuaLib.lua_rawgeti( L, index, 3 );					// |x|y|z
				vector.z = (float) LuaLib.lua_tonumber( L, -1 );	// |x|y|z
				LuaLib.lua_pop( L, 3 );								// |
			}

			return vector;
		}

		public UnityEngine.Vector4 ToVector4( int index )
		{
			UnityEngine.Vector4 vector = new UnityEngine.Vector4();

			if( LuaLib.lua_type( L, index ) == LuaTypes.LUA_TTABLE )
			{
				LuaLib.lua_rawgeti( L, index, 1 );					// |x
				vector.x = (float) LuaLib.lua_tonumber( L, -1 );	// |x
				LuaLib.lua_rawgeti( L, index, 2 );					// |x|y
				vector.y = (float) LuaLib.lua_tonumber( L, -1 );	// |x|y
				LuaLib.lua_rawgeti( L, index, 3 );					// |x|y|z
				vector.z = (float) LuaLib.lua_tonumber( L, -1 );	// |x|y|z
				LuaLib.lua_rawgeti( L, index, 4 );					// |x|y|z|w
				vector.w = (float) LuaLib.lua_tonumber( L, -1 );	// |x|y|z|w
				LuaLib.lua_pop( L, 4 );								// |
			}

			return vector;
		}

		public LuaFunction ToFunction( int index )
		{
			return new LuaFunction( this, index );
		}

		public object ToCSObject( int index )
		{
			int objectReference = LuaLib.wutlua_rawuserdata( L, index );
			object o;
			_objects.TryGetValue( objectReference, out o );

			return o;
		}

		public void PushObject( object o )
		{
			if( o == null )
			{
				LuaLib.lua_pushnil( L );
				return;
			}

			Type type = o.GetType();
			if( type.IsPrimitive )
			{
				switch( Type.GetTypeCode( type ) )
				{
					case TypeCode.Boolean:
					{
						PushObject( (bool) o );
						break;
					}
					default:
					{
						PushObject( Convert.ToDouble( o ) );
						break;
					}
				}
			}
			else if( type.IsValueType )
			{
				if( type == typeof( UnityEngine.Color ) )
				{
					PushObject( (UnityEngine.Color) o );
				}
				else if( type == typeof( UnityEngine.Quaternion ) )
				{
					PushObject( (UnityEngine.Quaternion) o );
				}
				else if( type == typeof( UnityEngine.Vector2 ) )
				{
					PushObject( (UnityEngine.Vector2) o );
				}
				else if( type == typeof( UnityEngine.Vector3 ) )
				{
					PushObject( (UnityEngine.Vector3) o );
				}
				else if( type == typeof( UnityEngine.Vector4 ) )
				{
					PushObject( (UnityEngine.Vector4) o );
				}
				else
				{
					// TODO
				}
			}
			else
			{
				if( type.IsArray )
				{
					PushArray( (Array) o );
				}
				else if( type == typeof( string ) )
				{
					LuaLib.lua_pushstring( L, (string) o );
				}
				else if( type == typeof( LuaCSFunction ) )
				{
					LuaLib.lua_pushcsfunction( L, (LuaCSFunction) o );
				}
				else if( type.IsSubclassOf( typeof( LuaObjectBase ) ) )
				{
					( (LuaObjectBase) o ).Push();
				}
				else
				{
					PushCSObject( o );
				}
			}
		}

		public void PushObject( bool o )
		{
			LuaLib.lua_pushboolean( L, o );
		}

		public void PushObject( double o )
		{
			LuaLib.lua_pushnumber( L, o );
		}

		public void PushObject( int o )
		{
			LuaLib.lua_pushinteger( L, o );
		}

		public void PushObject( long o )
		{
			LuaLib.lua_pushnumber( L, Convert.ToDouble( o ) );
		}

		public void PushObject( short o )
		{
			LuaLib.lua_pushinteger( L, o );
		}

		public void PushObject( uint o )
		{
			LuaLib.lua_pushnumber( L, Convert.ToDouble( o ) );
		}

		public void PushObject( ulong o )
		{
			LuaLib.lua_pushnumber( L, Convert.ToDouble( o ) );
		}

		public void PushObject( ushort o )
		{
			LuaLib.lua_pushinteger( L, o );
		}

		public void PushObject( UnityEngine.Color o )
		{
			LuaLib.lua_newtable( L );			// |t
			LuaLib.lua_pushnumber( L, o.r );	// |t|r
			LuaLib.lua_rawseti( L, -2, 1 );		// |t		// t[1] = r
			LuaLib.lua_pushnumber( L, o.g );	// |t|g
			LuaLib.lua_rawseti( L, -2, 2 );		// |t		// t[2] = g
			LuaLib.lua_pushnumber( L, o.b );	// |t|b
			LuaLib.lua_rawseti( L, -2, 3 );		// |t		// t[3] = b
			LuaLib.lua_pushnumber( L, o.a );	// |t|a
			LuaLib.lua_rawseti( L, -2, 4 );		// |t		// t[4] = a
		}

		public void PushObject( UnityEngine.Quaternion o )
		{
			LuaLib.lua_newtable( L );			// |t
			LuaLib.lua_pushnumber( L, o.x );	// |t|x
			LuaLib.lua_rawseti( L, -2, 1 );		// |t		// t[1] = x
			LuaLib.lua_pushnumber( L, o.y );	// |t|y
			LuaLib.lua_rawseti( L, -2, 2 );		// |t		// t[2] = y
			LuaLib.lua_pushnumber( L, o.z );	// |t|z
			LuaLib.lua_rawseti( L, -2, 3 );		// |t		// t[3] = z
			LuaLib.lua_pushnumber( L, o.w );	// |t|w
			LuaLib.lua_rawseti( L, -2, 4 );		// |t		// t[4] = w
		}

		public void PushObject( UnityEngine.Vector2 o )
		{
			LuaLib.lua_newtable( L );			// |t
			LuaLib.lua_pushnumber( L, o.x );	// |t|x
			LuaLib.lua_rawseti( L, -2, 1 );		// |t		// t[1] = x
			LuaLib.lua_pushnumber( L, o.y );	// |t|y
			LuaLib.lua_rawseti( L, -2, 2 );		// |t		// t[2] = y
		}

		public void PushObject( UnityEngine.Vector3 o )
		{
			LuaLib.lua_newtable( L );			// |t
			LuaLib.lua_pushnumber( L, o.x );	// |t|x
			LuaLib.lua_rawseti( L, -2, 1 );		// |t		// t[1] = x
			LuaLib.lua_pushnumber( L, o.y );	// |t|y
			LuaLib.lua_rawseti( L, -2, 2 );		// |t		// t[2] = y
			LuaLib.lua_pushnumber( L, o.z );	// |t|z
			LuaLib.lua_rawseti( L, -2, 3 );		// |t		// t[3] = z
		}

		public void PushObject( UnityEngine.Vector4 o )
		{
			LuaLib.lua_newtable( L );			// |t
			LuaLib.lua_pushnumber( L, o.x );	// |t|x
			LuaLib.lua_rawseti( L, -2, 1 );		// |t		// t[1] = x
			LuaLib.lua_pushnumber( L, o.y );	// |t|y
			LuaLib.lua_rawseti( L, -2, 2 );		// |t		// t[2] = y
			LuaLib.lua_pushnumber( L, o.z );	// |t|z
			LuaLib.lua_rawseti( L, -2, 3 );		// |t		// t[3] = z
			LuaLib.lua_pushnumber( L, o.w );	// |t|w
			LuaLib.lua_rawseti( L, -2, 4 );		// |t		// t[4] = w
		}

		public void PushObject( UnityEngine.Object o )
		{
			PushCSObject( o );
		}

		public void PushArray( Array array )
		{
			// TODO
		}

		public void PushCSObject( object o )
		{
			if( o == null )
			{
				LuaLib.lua_pushnil( L );
				return;
			}

			int refId;
			if( !_objectUserdataRefIds.TryGetValue( o, out refId ) )
			{
				LuaMetatable metatable = GetObjectMetatable( o.GetType() );

				int objectUserdataValue = _newObjectUserdataValue++;
				LuaLib.wutlua_newuserdata( L, objectUserdataValue );		// |ud
				metatable.Push();											// |ud|mt
				LuaLib.lua_setmetatable( L, -2 );							// |ud		// ud.metatable = mt

				refId = LuaLib.luaL_ref( L, LuaIndices.LUA_REGISTRYINDEX );	// |

				_objects[objectUserdataValue] = o;
				_objectUserdataRefIds[o] = refId;
			}
			LuaLib.lua_rawgeti( L, LuaIndices.LUA_REGISTRYINDEX, refId );	// |ud
		}

		public void GCCSObject( int index )
		{
			int objectReference = LuaLib.wutlua_rawuserdata( L, index );
			object o = _objects[objectReference];
			int refId = _objectUserdataRefIds[o];
			UnityEngine.Debug.Log( string.Format( "GC [{0}({1})]: {2}", objectReference, refId, o.ToString() ) );

			LuaLib.luaL_unref( L, LuaIndices.LUA_REGISTRYINDEX, refId );
			_objectUserdataRefIds.Remove( o );
			_objects.Remove( objectReference );
		}

		public void Close()
		{
			if( L != IntPtr.Zero )
			{
				LuaLib.lua_close( L );
			}
		}

 #region IDisposable members
 		public void Dispose()
 		{
			Close();

			_luaStates.Remove( L );

			GC.Collect();
			GC.WaitForPendingFinalizers();
 		}
 #endregion
 	}
}
