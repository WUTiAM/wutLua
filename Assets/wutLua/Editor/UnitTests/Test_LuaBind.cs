namespace wutLua.Test
{
	using NUnit.Framework;
	using System.Text;
	using UnityEngine;
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
		public void AccessStaticMemberFunction()
		{
			GameObject go = new GameObject( "go" );

			const string LUA_CODE = @"
GameObject = wutLua.ImportType( 'UnityEngine.GameObject' )

local go = GameObject.Find( 'go' )
if go == nil then
	return -1
end

return 0
			";
			object[] results = _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			Assert.AreEqual( 1, results.Length );
			Assert.AreEqual( 0, results[0] );
		}

		[Test]
		public void AccessStaticMemberFunctionInParent()
		{
			GameObject go = new GameObject( "go" );

			const string LUA_CODE = @"
GameObject = wutLua.ImportType( 'UnityEngine.GameObject' )

local go = GameObject.Find( 'go' )
GameObject.DestroyImmediate( go, true )
go = nil
			";
			_luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );
		}

		[Test]
		public void AccessConstructor()
		{
			const string LUA_CODE = @"
GameObject = wutLua.ImportType( 'UnityEngine.GameObject' )

local go = GameObject( 'go' )
if go == nil or go.name ~= 'go' then
	return -1
end

local go2 = GameObject()
if go2 == nil then
	return -2
end

return 0
			";
			object[] results = _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			Assert.AreEqual( 1, results.Length );
			Assert.AreEqual( 0, results[0] );
		}

		[Test]
		public void AccessMemberFunctionInParent()
		{
			const string LUA_CODE = @"
GameObject = wutLua.ImportType( 'UnityEngine.GameObject' )

local go = GameObject( 'go' )
local name = go:ToString()
local instanceId = go:GetInstanceID()

return name, instanceId
			";
			object[] results = _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			Assert.AreEqual( 2, results.Length );
			Assert.AreEqual( "go (UnityEngine.GameObject)", results[0] );
			Assert.IsTrue( results[1] is double );
		}

		[Test]
		public void PassTypeAsParam()
		{
			Camera go = new Camera();

			const string LUA_CODE = @"
Camera = wutLua.ImportType( 'UnityEngine.Camera' )
Object = wutLua.ImportType( 'UnityEngine.Object' )

local camera = Object.FindObjectOfType( Camera )
local cameraGO = camera.gameObject
local cameraComponent = cameraGO:GetComponent( Camera )

return camera.enabled, camera, cameraComponent, camera == cameraComponent
			";
			object[] results = _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			Assert.AreEqual( 4, results.Length );
			Assert.AreEqual( true, results[0] );
			Assert.AreEqual( true, results[1] == results[2] );
			Assert.AreEqual( true, results[3] );
		}

		[Test]
		public void AccessMemberProperty()
		{
			const string LUA_CODE = @"
GameObject = wutLua.ImportType( 'UnityEngine.GameObject' )

local go = GameObject( 'go' )
local name = go.name
go.name = 'gogogo'

return go, name
			";
			object[] results = _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			Assert.AreEqual( 2, results.Length );
			GameObject go = results[0] as GameObject;
			Assert.IsTrue( go != null && go.name == "gogogo" );
			Assert.AreEqual( "go", results[1] );
		}

		[Test]
		[ExpectedException( typeof( LuaException ) )]
		public void AccessInexistedMemberProperty()
		{
			const string LUA_CODE = @"
GameObject = wutLua.ImportType( 'UnityEngine.GameObject' )

local go = GameObject( 'go' )
local notExists = go.notExists
go.notExists = 123
			";
			_luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );
		}

		[Test]
		public void TriggerObjectGC()
		{
			const string LUA_CODE = @"
GameObject = wutLua.ImportType( 'UnityEngine.GameObject' )

local go = GameObject()
local go2 = GameObject( 'go2' )

go = nil
go2 = nil

collectgarbage()
			";
			_luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

//			Assert.AreEqual( 0, _luaState._objects.Count );
		}
	}
}
