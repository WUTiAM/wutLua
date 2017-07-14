using UnityEngine;
using wuanLua;

public class HelloWorld : MonoBehaviour
{
	public TextAsset LuaScript;

	LuaState _luaState;

	void Awake()
	{
//		_luaState = new LuaState();
//
//		_luaState.SetObject( "gb", true );
//
//		_luaState.DoBuffer( LuaScript.bytes, LuaScript.name );
//
//		double gn = (double) _luaState.Get( "gn" );
//		string gs = (string) _luaState.Get( "gs" );
//		Debug.Log( string.Format( "gn: {0}, gs: '{1}'", gn, gs ) );
//
//		LuaTable gt = _luaState.Get( "gt" ) as LuaTable;
//		Debug.Log( string.Format( "gt.n: {0}, gt.s: '{1}'", gt["n"], gt["s"] ) );
//
//		bool gtTB = (bool) _luaState.Get( "gt.t.b" );
//		Debug.Log( string.Format( "gt.t.b: {0}", gtTB ) );
//
//		LuaFunction gf = _luaState.Get( "gf" ) as LuaFunction;
//		object[] returnValues = gf.Call( 1, 2, gameObject );
//		Debug.Log( string.Format( "gf(): {0}, {1}", returnValues[0], returnValues[1] ) );
	}
}
