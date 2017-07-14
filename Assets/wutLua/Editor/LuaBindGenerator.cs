namespace wuanLua
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.IO;
	using System.Reflection;
	using System.Text;
	using System.Text.RegularExpressions;
	using UnityEditor;
	using UnityEngine;

	public static class LuaBindGenerator
	{
		class Members
		{
			public List<ConstructorInfo> constructors = new List<ConstructorInfo>();
			public Dictionary<string, List<MethodInfo>> staticMethods = new Dictionary<string, List<MethodInfo>>();
			public List<PropertyInfo> staticProperties = new List<PropertyInfo>();
			public Dictionary<string, List<MethodInfo>> methods = new Dictionary<string, List<MethodInfo>>();
			public List<PropertyInfo> properties = new List<PropertyInfo>();
		}

		static readonly string _GENERATE_ROOT_PATH = Path.Combine( Application.dataPath, Config.LuaBindGeneratedCodePath );

		static StreamWriter _currentStreamWriter;
		static int _currentIndent = 0;

		static void GenerateCodes()
		{
			if( _IsEditorCompiling() )
				return;

			_DeleteAllInDirectory( _GENERATE_ROOT_PATH );

			List<Type> types = new List<Type>();
			types.AddRange( Config.LuaBindList );

			foreach( Type type in types )
			{
				Members members = _CollectTypeMembers( type );
				_GenerateCodeForType( type, members );
			}

			_GenerateLuaBindingsCode( types );

			_currentStreamWriter = null;
			_currentIndent = 0;

			AssetDatabase.Refresh();
		}

		static Members _CollectTypeMembers( Type type )
		{
			Members members = new Members();

			foreach( PropertyInfo pi in type.GetProperties() )
			{
				if( _IsIgnoredProperty( pi ) )
					continue;

				if( ( pi.GetGetMethod() != null && pi.GetGetMethod().IsStatic ) || ( pi.GetSetMethod() != null && pi.GetSetMethod().IsStatic ) )
				{
					members.staticProperties.Add( pi );
				}
				else
				{
					members.properties.Add( pi );
				}
			}

			foreach( ConstructorInfo ci in type.GetConstructors( BindingFlags.Instance | BindingFlags.Public ) )
			{
				if( _IsIgnoredConstructor( ci ) )
					continue;

				members.constructors.Add( ci );
			}

			foreach( MethodInfo mi in type.GetMethods( BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static ) )
			{
				if( _IsIgnoredMethod( mi ) )
					continue;

				if( mi.IsStatic )
				{
					// Ignore static properties' getter and setter
					if( members.staticProperties.Any( pi => pi.GetGetMethod() == mi || pi.GetSetMethod() == mi ) )
						continue;

					List<MethodInfo> sameNameMethods;
					if( !members.staticMethods.TryGetValue( mi.Name, out sameNameMethods ) )
					{
						sameNameMethods = new List<MethodInfo>();
						members.staticMethods.Add( mi.Name, sameNameMethods );
					}
					sameNameMethods.Add( mi );
				}
				else
				{
					// Ignore properties' getter and setter
					if( members.properties.Any( pi => pi.GetGetMethod() == mi || pi.GetSetMethod() == mi ) )
						continue;

					List<MethodInfo> sameNameMethods;
					if( !members.methods.TryGetValue( mi.Name, out sameNameMethods ) )
					{
						sameNameMethods = new List<MethodInfo>();
						members.methods.Add( mi.Name, sameNameMethods );
					}
					sameNameMethods.Add( mi );
				}
			}

			return members;
		}

		static bool _IsEditorCompiling()
		{
			if( EditorApplication.isCompiling )
			{
				EditorUtility.DisplayDialog(
					"Error",
					"The Unity Editor is compiling, please wait and try again later.",
					"Ok" );
				return true;
			}
			return false;
		}

		static bool _IsIgnoredProperty( PropertyInfo pi )
		{
			object[] attributes = pi.GetCustomAttributes( true );
			for( int i = 0; i < attributes.Length; ++i )
			{
				Type t = attributes[i].GetType();
				if( t == typeof( ObsoleteAttribute )
					|| t == typeof( LuaBindIgnoreAttribute ) )
				{
					return true;
				}
			}

			return false;
		}

		static bool _IsIgnoredConstructor( ConstructorInfo ci )
		{
			object[] attributes = ci.GetCustomAttributes( true );
			for( int i = 0; i < attributes.Length; ++i )
			{
				Type t = attributes[i].GetType();
				if( t == typeof( ObsoleteAttribute )
					|| t == typeof( LuaBindIgnoreAttribute ) )
				{
					return true;
				}
			}

			return false;
		}

		static bool _IsIgnoredMethod( MethodInfo mi )
		{
			// Ignore generic methods
			if( mi.IsGenericMethod || mi.ContainsGenericParameters )
				return true;

			object[] attributes = mi.GetCustomAttributes( true );
			for( int i = 0; i < attributes.Length; ++i )
			{
				Type t = attributes[i].GetType();
				if( t == typeof( ObsoleteAttribute )
					|| t == typeof( LuaBindIgnoreAttribute )
					|| t == typeof( MonoPInvokeCallbackAttribute ) )
				{
					return true;
				}
			}

			string memberName = mi.Name;
			if( memberName.StartsWith( "op_", StringComparison.Ordinal ) // operator
				|| memberName.StartsWith( "add_", StringComparison.Ordinal )
				|| memberName.StartsWith( "remove_", StringComparison.Ordinal )
				|| memberName.StartsWith( "get_", StringComparison.Ordinal )
			    || memberName.StartsWith( "set_", StringComparison.Ordinal )
			    || memberName.Equals( "Clone", StringComparison.Ordinal )
				|| memberName.Equals( "CopyTo", StringComparison.Ordinal )
				|| memberName.Equals( "Equals", StringComparison.Ordinal )
				|| memberName.Equals( "GetEnumerator", StringComparison.Ordinal )
				|| memberName.Equals( "GetHashCode", StringComparison.Ordinal )
				|| memberName.Equals( "GetType", StringComparison.Ordinal ) )
			{
				return true;
			}

			return false;
		}

		static void _GenerateCodeForType( Type type, Members members )
		{
			string className = "LuaBind_" + type.FullName.Replace( '.', '_' );
			string path = Path.Combine( _GENERATE_ROOT_PATH, className + ".cs" );

			using( _currentStreamWriter = new StreamWriter( path, false, Encoding.UTF8 ) )
			{
				_currentIndent = 0;

				_WriteLine( "namespace wuanLuaBind" );
				_WriteLine( "{" );
				{
					_WriteLine( "using System;" );
					_WriteLine( "using wuanLua;" );
					_WriteLine( "" );
					_WriteLine( "public class {0} : LuaBindBase", className );
					_WriteLine( "{" );
					{
						_WriteRegister( type, members );

						_WriteConstructorAccessor( members.constructors );
						foreach( var pair in members.staticMethods )
						{
							_WriteMethodAccessor( pair.Key, true, pair.Value );
						}
						foreach( PropertyInfo pi in members.staticProperties )
						{
							_WritePropertyAccessor( pi );
						}
						foreach( var pair in members.methods )
						{
							_WriteMethodAccessor( pair.Key, false, pair.Value );
						}
						foreach( PropertyInfo pi in members.properties )
						{
							_WritePropertyAccessor( pi );
						}
					}
					_WriteLine( "}" );
				}
				_WriteLine( "}" );
			}
		}

		static void _WriteRegister( Type type, Members members )
		{
			_WriteLine( "public static void Register()" );
			_WriteLine( "{" );
			{
				_WriteLine( "_BeginTypeMembers( typeof( {0} ), {1} );",
					type.FullName,
					members.constructors.Count > 0 ? "_Constructor" : "null" );
				if( type.IsEnum )
				{
					foreach( string enumName in Enum.GetNames( type ) )
					{
						_WriteLine( "_AddMember( \"{0}\", {1} );",
							enumName,
							Convert.ToInt32( Enum.Parse( type, enumName ) ) );
					}
				}
				else
				{
					foreach( var pair in members.staticMethods )
					{
						_WriteLine( "_AddMember( \"{0}\", _static_{0} );", pair.Key );
					}
					foreach( PropertyInfo pi in members.staticProperties )
					{
						_WriteLine( "_AddMember( \"{0}\", {1}, {2} );",
							pi.Name,
							pi.GetGetMethod() != null ? string.Format( "_static_{0}_Getter", pi.Name ) : "null",
							pi.GetSetMethod() != null ? string.Format( "_static_{0}_Setter", pi.Name ) : "null" );
					}
					_WriteLine( "" );
	
					_WriteLine( "_BeginInstanceMembers( typeof( {0} ) );", type.FullName );
					foreach( var pair in members.methods )
					{
						_WriteLine( "_AddMember( \"{0}\", _{0} );", pair.Key );
					}
					foreach( PropertyInfo pi in members.properties )
					{
						_WriteLine( "_AddMember( \"{0}\", {1}, {2} );",
							pi.Name,
							pi.GetGetMethod() != null ? string.Format( "_{0}_Getter", pi.Name ) : "null",
							pi.GetSetMethod() != null ? string.Format( "_{0}_Setter", pi.Name ) : "null" );
					}
					
				}
				_WriteLine( "" );

				_WriteLine( "_RegisterType( typeof( {0} ) );", type.FullName );
			}
			_WriteLine( "}" );
		}

		static void _WriteConstructorAccessor( List<ConstructorInfo> overridedMethods )
		{
			if( overridedMethods.Count == 0 )
				return;

			_WriteLine( "" );
			_WriteLine( "[MonoPInvokeCallback( typeof( LuaCSFunction ) )]" );
			_WriteLine( "static int _Constructor( IntPtr L )" );
			_WriteLine( "{" );
			{
				_WriteLine( "LuaState luaState = LuaState.Get( L );" );
				_WriteLine( "" );
				_WriteLine( "try" );
				_WriteLine( "{" );
				{
					if( overridedMethods.Count > 1 )
					{
						_WriteLine( "int argc = LuaLib.lua_gettop( L );" );
					}

					// Sort by param count
					bool isEveryParamCountUnique = true;
					overridedMethods.Sort( delegate( ConstructorInfo ci1, ConstructorInfo ci2 )
					{
						if( ci1 == ci2 )
							return 0;

						ParameterInfo[] ci1ParamsInfo = ci1.GetParameters();
						ParameterInfo[] ci2ParamsInfo = ci2.GetParameters();
						int ci1ParamCount = ci1ParamsInfo.Count();
						int ci2ParamCount = ci2ParamsInfo.Count();
						if( ci1ParamCount > 0 && _IsParamArray( ci1ParamsInfo.Last() ) )
						{
							--ci1ParamCount;
						}
						if( ci2ParamCount > 0 && _IsParamArray( ci2ParamsInfo.Last() ) )
						{
							--ci2ParamCount;
						}

						if( isEveryParamCountUnique && ci1ParamCount == ci2ParamCount )
						{
							isEveryParamCountUnique = false;
						}

						// Put methods with param array to the last
						if( ci1ParamCount > 0 && _IsParamArray( ci1ParamsInfo.Last() ) )
						{
							ci1ParamCount *= 100;
						}
						if( ci2ParamCount > 0 && _IsParamArray( ci2ParamsInfo.Last() ) )
						{
							ci2ParamCount *= 100;
						}

						return ci1ParamCount - ci2ParamCount;
					});

					foreach( ConstructorInfo ci in overridedMethods )
					{
						ParameterInfo[] parametersInfo = ci.GetParameters();
						bool methodHasParameters = parametersInfo.Any();
						string tc = "";
						string p = "";
						for( int i = 0; i < parametersInfo.Count(); ++i )
						{
							ParameterInfo parameterInfo = parametersInfo[i];

							if( !isEveryParamCountUnique )
							{
								tc += string.Format( " && {0}", _GetLuaTypeCheckCode( i + 2, parameterInfo ) );
							}

							if( i > 0 )
							{
								p += ", ";
							}
							p += string.Format( "arg{0}", i + 2 );
						}

						if( overridedMethods.Count > 1 )
						{
							if( !parametersInfo.Any() || !_IsParamArray( parametersInfo.Last() ) )
							{
								_WriteLine( "if( argc == {0}{1} )",
									parametersInfo.Count() + 1,
									tc );
							}
							else
							{
								_WriteLine( "if( argc >= {0}{1} )",
									parametersInfo.Count(),
									tc );
							}
							_WriteLine( "{" );
						}
						{
							for( int i = 0; i < parametersInfo.Count(); ++i )
							{
								ParameterInfo pi = parametersInfo[i];
								_WriteLine( _GetToObjectCode( i + 2, pi ) );
							}

							if( !methodHasParameters )
							{
								_WriteLine( "{0} instance = new {0}();",
									ci.DeclaringType );
							}
							else
							{
								_WriteLine( "{0} instance = new {0}( {1} );",
									ci.DeclaringType,
									p );
							}
						}
						_WriteLine( "luaState.PushObject( instance );" );
						_WriteLine( "" );
						_WriteLine( "return 1;" );
						if( overridedMethods.Count > 1 )
						{
							_WriteLine( "}" );
						}
					}
				}
				_WriteLine( "}" );
				_WriteLine( "catch( Exception e )" );
				_WriteLine( "{" );
				{
					_WriteLine( "return LuaLib.luaL_error( L, e.ToString() );" );
				}
				_WriteLine( "}" );
			}
			if( overridedMethods.Count > 1 )
			{
				_WriteLine( "" );
				_WriteLine( "return LuaLib.luaL_error( L, \"Invalid arguments to {0}'s constructor!\" );",
					overridedMethods[0].DeclaringType );
			}
			_WriteLine( "}" );
		}

		static void _WriteMethodAccessor( string methodName, bool isStaticMethod, List<MethodInfo> overridedMethods )
		{
			int offset = isStaticMethod ? 0 : 1;

			_WriteLine( "" );
			_WriteLine( "[MonoPInvokeCallback( typeof( LuaCSFunction ) )]" );
			_WriteLine( "static int _{0}{1}( IntPtr L )", isStaticMethod ? "static_" : "", methodName );
			_WriteLine( "{" );
			{
				_WriteLine( "LuaState luaState = LuaState.Get( L );" );
				_WriteLine( "" );
				_WriteLine( "try" );
				_WriteLine( "{" );
				{
					if( overridedMethods.Count > 1 )
					{
						_WriteLine( "int argc = LuaLib.lua_gettop( L );" );
					}

					if( !isStaticMethod )
					{
						_WriteLine( "{0} self = luaState.ToCSObject( 1 ) as {0};", overridedMethods[0].DeclaringType );
					}

					// Sort by param count
					bool isEveryParamCountUnique = true;
					overridedMethods.Sort( delegate( MethodInfo mi1, MethodInfo mi2 )
					{
						if( mi1 == mi2 )
							return 0;

						ParameterInfo[] mi1ParamsInfo = mi1.GetParameters();
						ParameterInfo[] mi2ParamsInfo = mi2.GetParameters();
						int mi1ParamCount = mi1ParamsInfo.Count();
						int mi2ParamCount = mi2ParamsInfo.Count();
						if( mi1ParamCount > 0 && _IsParamArray( mi1ParamsInfo.Last() ) )
						{
							--mi1ParamCount;
						}
						if( mi2ParamCount > 0 && _IsParamArray( mi2ParamsInfo.Last() ) )
						{
							--mi2ParamCount;
						}

						if( isEveryParamCountUnique && mi1ParamCount == mi2ParamCount )
						{
							isEveryParamCountUnique = false;
						}

						// Put methods with param array to the last
						if( mi1ParamCount > 0 && _IsParamArray( mi1ParamsInfo.Last() ) )
						{
							mi1ParamCount *= 100;
						}
						if( mi2ParamCount > 0 && _IsParamArray( mi2ParamsInfo.Last() ) )
						{
							mi2ParamCount *= 100;
						}

						return mi1ParamCount - mi2ParamCount;
					});

					foreach( MethodInfo mi in overridedMethods )
					{
						ParameterInfo[] parametersInfo = mi.GetParameters();
						bool methodHasParameters = parametersInfo.Any();
						bool methodHasReturn = ( mi.ReturnType != typeof( void ) );
						string tc = "";
						string p = "";
						string r = "";
						for( int i = 0; i < parametersInfo.Count(); ++i )
						{
							ParameterInfo pi = parametersInfo[i];

							if( !isEveryParamCountUnique )
							{
								tc += string.Format( " && {0}", _GetLuaTypeCheckCode( i + 1 + offset, pi ) );
							}

							if( i > 0 )
							{
								p += ", ";
							}
							if( pi.ParameterType.IsByRef && pi.IsOut )
							{
								p += "out ";
							}
							else if( pi.ParameterType.IsByRef )
							{
								p += "ref ";
							}
							p += string.Format( "arg{0}", i + 1 + offset );
						}
						if( methodHasReturn )
						{
							r = string.Format( "{0} r = ", mi.ReturnType );
						}

						if( overridedMethods.Count > 1 )
						{
							if( !parametersInfo.Any() || !_IsParamArray( parametersInfo.Last() ) )
							{
								_WriteLine( "if( argc == {0}{1} )",
									parametersInfo.Count() + offset,
									tc );
							}
							else
							{
								_WriteLine( "if( argc >= {0}{1} )",
									parametersInfo.Count() - 1 + offset,
									tc );
							}
							_WriteLine( "{" );
						}
						{
							for( int i = 0; i < parametersInfo.Count(); ++i )
							{
								ParameterInfo pi = parametersInfo[i];
								_WriteLine( _GetToObjectCode( i + 1 + offset, pi ) );
							}

							if( !methodHasParameters )
							{
								_WriteLine( "{2}{0}.{1}();",
									isStaticMethod ? mi.DeclaringType.ToString() : "self",
									mi.Name,
									r );
							}
							else
							{
								_WriteLine( "{3}{0}.{1}( {2} );",
									isStaticMethod ? mi.DeclaringType.ToString() : "self",
									mi.Name,
									p,
									r );
							}

						}
						if( methodHasReturn )
						{
							_WriteLine( "luaState.PushObject( r );" );
						}
						_WriteLine( "" );
						_WriteLine( "return {0};", methodHasReturn ? 1 : 0 );
						if( overridedMethods.Count > 1 )
						{
							_WriteLine( "}" );
						}
					}
				}
				_WriteLine( "}" );
				_WriteLine( "catch( Exception e )" );
				_WriteLine( "{" );
				{
					_WriteLine( "return LuaLib.luaL_error( L, e.ToString() );" );
				}
				_WriteLine( "}" );
			}
			if( overridedMethods.Count > 1 )
			{
				_WriteLine( "" );
				_WriteLine( "return LuaLib.luaL_error( L, \"Invalid arguments to method {0}!\" );", methodName );

			}
			_WriteLine( "}" );
		}

		static void _WritePropertyAccessor( PropertyInfo propertyInfo )
		{
			if( propertyInfo.GetGetMethod() != null )
			{
				MethodInfo mi = propertyInfo.GetGetMethod();

				_WriteLine( "" );
				_WriteLine( "[MonoPInvokeCallback( typeof( LuaCSFunction ) )]" );
				_WriteLine( "static int {0}_{1}_Getter( IntPtr L )",
					mi.IsStatic ? "_static" : "",
					propertyInfo.Name );
				_WriteLine( "{" );
				{
					_WriteLine( "LuaState luaState = LuaState.Get( L );" );
					_WriteLine( "" );
					_WriteLine( "try" );
					_WriteLine( "{" );
					{
						if( !mi.IsStatic )
						{
							_WriteLine( "{0} self = luaState.ToCSObject( 1 ) as {0};", propertyInfo.DeclaringType );
						}

						ParameterInfo[] parametersInfo = propertyInfo.GetIndexParameters();
						// It's an indexer
						if( parametersInfo.Any() )
						{
							ParameterInfo pi = parametersInfo[0];
							_WriteLine( _GetToObjectCode( 2, pi ) );
							_WriteLine( "luaState.PushObject( self[arg2] );" );
						}
						// Normal property
						else
						{
							_WriteLine( "luaState.PushObject( {0}.{1} );",
								mi.IsStatic ? propertyInfo.DeclaringType.ToString() : "self",
								propertyInfo.Name );
						}
						_WriteLine( "" );
						_WriteLine( "return 1;" );
					}
					_WriteLine( "}" );
					_WriteLine( "catch( Exception e )" );
					_WriteLine( "{" );
					{
						_WriteLine( "return LuaLib.luaL_error( L, e.ToString() );" );
					}
					_WriteLine( "}" );
				}
				_WriteLine( "}" );
			}
			if( propertyInfo.GetSetMethod() != null )
			{
				MethodInfo mi = propertyInfo.GetSetMethod();

				_WriteLine( "" );
				_WriteLine( "[MonoPInvokeCallback( typeof( LuaCSFunction ) )]" );
				_WriteLine( "static int {0}_{1}_Setter( IntPtr L )",
					mi.IsStatic ? "_static" : "",
					propertyInfo.Name );
				_WriteLine( "{" );
				{
					_WriteLine( "LuaState luaState = LuaState.Get( L );" );
					_WriteLine( "" );
					_WriteLine( "try" );
					_WriteLine( "{" );
					{
						if( !mi.IsStatic )
						{
							_WriteLine( "{0} self = luaState.ToCSObject( 1 ) as {0};", propertyInfo.DeclaringType );
						}
						_WriteLine( "{0}.{1} = ({2}) luaState.ToObject( 2 );",
							mi.IsStatic ? propertyInfo.DeclaringType.ToString() : "self",
							propertyInfo.Name,
							propertyInfo.PropertyType );
						_WriteLine( "" );
						_WriteLine( "return 0;" );
					}
					_WriteLine( "}" );
					_WriteLine( "catch( Exception e )" );
					_WriteLine( "{" );
					{
						_WriteLine( "return LuaLib.luaL_error( L, e.ToString() );" );
					}
					_WriteLine( "}" );
				}
				_WriteLine( "}" );
			}
		}

		static string _GetLuaTypeCheckCode( int i, ParameterInfo pi )
		{
			Type paramType = pi.ParameterType;

			if( _IsParamArray( pi ) )
			{
				return string.Format(
					"( LuaLib.lua_type( L, {0} ) == LuaTypes.LUA_TNONE || luaState.CheckParamArray( {0}, argc - {1}, typeof( {2} ) ) )",
					i,
					i - 1,
					_GetTypeName( paramType.GetElementType() ) );
			}
			if( paramType == typeof( Type ) )
			{
				return string.Format( "LuaLib.lua_type( L, {0} ) == LuaTypes.LUA_TTABLE", i );
			}
			if( paramType.IsEnum )
			{
				return string.Format( "LuaLib.lua_type( L, {0} ) == LuaTypes.LUA_TNUMBER", i );
			}
			if( paramType == typeof( bool ) )
			{
				return string.Format( "LuaLib.lua_type( L, {0} ) == LuaTypes.LUA_TBOOLEAN", i );
			}
			if( paramType.IsPrimitive )
			{
				return string.Format( "LuaLib.lua_type( L, {0} ) == LuaTypes.LUA_TNUMBER", i );
			}
			if( paramType == typeof( string ) )
			{
				return string.Format( "_CheckLuaType( L, {0}, LuaTypes.LUA_TSTRING, LuaTypes.LUA_TNIL )", i );
			}

			return string.Format( "luaState.CheckType( {0}, typeof( {1} ) )", i, _GetTypeName( paramType ) );
		}

		static string _GetToObjectCode( int i, ParameterInfo pi )
		{
			Type paramType = pi.ParameterType;

			if( _IsParamArray( pi ) )
			{
				return string.Format( "{0} arg{1}; luaState.ToParamArray<{2}>( {1}, argc - {3}, out arg{1} );",
					_GetTypeName( paramType ),
					i,
					paramType.GetElementType(),
					i - 1 );
			}
			if( paramType == typeof( Type ) )
			{
				return string.Format( "{0} arg{1}; luaState.ToType( {1}, out arg{1} );", _GetTypeName( paramType ), i );
			}
			if( paramType.IsEnum )
			{
				return string.Format( "{0} arg{1}; luaState.ToEnum( {1}, out arg{1} );", _GetTypeName( paramType ), i );
			}
			if( paramType == typeof( bool ) )
			{
				return string.Format( "{0} arg{1} = LuaLib.lua_toboolean( L, {1} );", paramType, i );
			}
			if( paramType.IsPrimitive )
			{
				return string.Format( "{0} arg{1} = ({0}) LuaLib.lua_tonumber( L, {1} );", paramType, i );
			}
			if( paramType == typeof( string ) )
			{
				return string.Format( "{0} arg{1} = LuaLib.lua_tostring( L, {1} );", paramType, i );
			}

			return string.Format( "{0} arg{1} = ({0}) luaState.ToObject( {1} );", _GetTypeName( paramType ), i );
		}

		static string _GetTypeName( Type type )
		{
			if( type.IsGenericType )
			{
				return _GetGenericTypeName( type );
			}
			else
			{
				if( type.IsByRef )
				{
					type = type.GetElementType();
				}
				return type.ToString().Replace( "+", "." );
			}
		}

		static string _GetGenericTypeName( Type type )
		{
			string typeName = type.FullName; // e.g. System.Collections.Generic.List`1[[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089]]
			if( typeName.IndexOf( '[' ) > 0 )
			{
				typeName = typeName.Substring( 0, typeName.IndexOf( '[' ) );
			}
			typeName = typeName.Replace( "+", "." );

			string gaName = "<";
			Type[] genericArguments = type.GetGenericArguments();
			for( int i = 0; i < genericArguments.Length; ++i )
			{
				gaName += _GetTypeName( genericArguments[i] );

				if( i < genericArguments.Length - 1 )
				{
					gaName += ", ";
				}
			}
			gaName += '>';
			typeName = Regex.Replace( typeName, @"`\d", gaName);

			return typeName;
		}

		static bool _IsParamArray( ParameterInfo pi )
		{
			return pi.GetCustomAttributes( typeof( ParamArrayAttribute ), false ).Any();
		}

		static void _GenerateLuaBindingsCode( List<Type> types )
		{
			string path = Path.Combine( _GENERATE_ROOT_PATH, "LuaBindings.cs" );

			using( _currentStreamWriter = new StreamWriter( path, false, Encoding.UTF8 ) )
			{
				_currentIndent = 0;

				_WriteLine( "namespace wuanLua" );
				_WriteLine( "{" );
				{
					_WriteLine( "using wuanLuaBind;" );
					_WriteLine( "" );
					_WriteLine( "public partial class LuaBindings" );
					_WriteLine( "{" );
					{
						_WriteLine( "partial void _InitializeBindings()" );
						_WriteLine( "{" );
						{
							_WriteLine( "LuaBindBase.LuaState = _luaState;" );
							_WriteLine( "" );

							foreach( Type type in types )
							{
								_WriteLine( "LuaBind_{0}.Register();", type.FullName.Replace( '.', '_' ) );
							}
						}
						_WriteLine( "}" );
					}
					_WriteLine( "}" );
				}
				_WriteLine( "}" );
			}
		}

		static void _WriteLine( string line, params object[] args )
		{
			if( args.Length > 0 )
			{
				line = string.Format( line, args );
			}

			if( line.EndsWith( "}", StringComparison.Ordinal ) )
			{
				--_currentIndent;
			}

			if( !string.IsNullOrEmpty( line ) )
			{
				for( int i = 1; i <= _currentIndent; ++i )
				{
					_currentStreamWriter.Write( '\t' );
				}
			}

			_currentStreamWriter.WriteLine( line );

			if( line.StartsWith( "{", StringComparison.Ordinal ) )
			{
				++_currentIndent;
			}
		}

		static void _DeleteAllInDirectory( string dir )
		{
			if( !Directory.Exists( dir ) )
				return;

			foreach( var file in Directory.GetFiles( dir ) )
			{
				File.Delete( file );
			}

			foreach( var directory in Directory.GetDirectories( dir ) )
			{
				Directory.Delete( directory, true );
			}
		}

		// -------------------------------------------------------------------------------------------------------------

		[MenuItem( "wutLua/Generate Lua Bind Files", false, 1 )]
		public static void GenerateLuaBindFiles()
		{
			GenerateCodes();
		}
	}
}
