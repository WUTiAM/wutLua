using UnityEngine;

namespace wutLua.Test
{
	using NUnit.Framework;
	using System.Runtime.CompilerServices;
	using System.Text;
	using wutLua;

	[TestFixture]
	public class Test_LuaBind
	{
		LuaState _luaState;

		[TestFixtureSetUp]
		public void Start()
		{
		}

		[TestFixtureTearDown]
		public void End()
		{
		}

		[SetUp]
		public void PreTest()
		{
			_luaState = new LuaState();
		}

		[TearDown]
		public void PostTest()
		{
			_luaState.Dispose();
			_luaState = null;
		}

		[Test]
		public void LuaAccessStaticMemberFunction()
		{
			GameObject go = new GameObject( "go" );

			const string LUA_CODE = @"
wutLua.ImportType( 'UnityEngine.GameObject' )

local go = UnityEngine.GameObject.Find( 'go' )
UnityEngine.GameObject.DestroyImmediate( go, true )
go = nil
			";
			_luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );
		}

		[Test]
		public void LuaAccessConstructor()
		{
			const string LUA_CODE = @"
wutLua.ImportType( 'UnityEngine.GameObject' )

local go = UnityEngine.GameObject( 'go' )
if go == nil or go.name ~= 'go' then
	return -1
end

local go2 = UnityEngine.GameObject()
if go2 == nil then
	return -2
end

return 0
			";
			object[] results = _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			Assert.AreEqual( 1, results.Length );
			Assert.AreEqual( 0, (int)(double) results[0] );
		}

		[Test]
		public void LuaTriggerObjectGC()
		{
			const string LUA_CODE = @"
wutLua.ImportType( 'UnityEngine.GameObject' )

local go = UnityEngine.GameObject()
local go2 = UnityEngine.GameObject( 'go2' )

go = nil
go2 = nil

collectgarbage()
			";
			_luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

//			Assert.AreEqual( 0, _luaState._objects.Count );
		}
	}
}
