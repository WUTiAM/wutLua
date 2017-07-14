namespace wuanLua.Test
{
	using NUnit.Framework;
	using System.Text;
	using UnityEngine;
	using wuanLua;

	[TestFixture]
	public class Test_LuaBind
	{
		LuaState _luaState;

		[OneTimeSetUp]
		public void Start()
		{
		}

		[OneTimeTearDown]
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
			Assert.AreEqual( "go", go.name );

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
			Assert.AreEqual( "go", go.name );

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
			GameObject cameraGO = new GameObject( "Camera" );
			cameraGO.AddComponent<Camera>();

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
		public void PassEnumAsParam()
		{
			const string LUA_CODE = @"
GameObject = wutLua.ImportType( 'UnityEngine.GameObject' )
Space = wutLua.ImportType( 'UnityEngine.Space' )
Transform = wutLua.ImportType( 'UnityEngine.Transform' )

local go = GameObject( 'Parent' )
local transform = go.transform
transform:Rotate( 0, 0, 90, Space.Self )

return transform, Space.World
			";
			object[] results = _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			Assert.AreEqual( 2, results.Length );
			Assert.AreEqual( 90f, ( results[0] as Transform ).eulerAngles.z );
			Assert.AreEqual( (int) Space.World, results[1] );
		}

		[Test]
		public void PassParamArray()
		{
			const string LUA_CODE = @"
Animation = wutLua.ImportType( 'UnityEngine.Animation' )
Camera = wutLua.ImportType( 'UnityEngine.Camera' )
GameObject = wutLua.ImportType( 'UnityEngine.GameObject' )

local cameraGO = GameObject( 'Camera', Camera, Animation )
local cameraComponent = cameraGO:GetComponent( Camera )
local cameraComponent2 = cameraGO:GetComponent( 'Camera' )

return cameraGO, cameraComponent, cameraComponent2, cameraComponent == cameraComponent2
			";
			object[] results = _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );
			GameObject cameraGO = results[0] as GameObject;

			Assert.AreEqual( 4, results.Length );
			Assert.AreSame( results[1], results[2] );
			Assert.AreSame( results[1], cameraGO.GetComponent<Camera>() );
			Assert.AreEqual( true, results[3] );
			Assert.IsNotNull( cameraGO.GetComponent<Animation>() );
		}

		[Test]
		public void PassArray()
		{
			const string LUA_CODE = @"
GameObject = wutLua.ImportType( 'UnityEngine.GameObject' )
Transform = wutLua.ImportType( 'UnityEngine.Transform' )

local parentGO = GameObject( 'Parent' )
local childGO = GameObject( 'Child' )
local child1GO = GameObject.Instantiate( childGO, parentGO.transform )
local child2GO = GameObject.Instantiate( childGO, parentGO.transform )
local child3GO = GameObject.Instantiate( childGO, child1GO.transform )
local childrenTransforms = parentGO:GetComponentsInChildren( Transform )

return childrenTransforms.Length
			";
			object[] results = _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			Assert.AreEqual( 1, results.Length );
			Assert.AreEqual( 4, results[0] );
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
			Assert.AreEqual( "gogogo", go.name );
			Assert.AreEqual( "go", results[1] );
		}

		[Test]
		//[ExpectedException( typeof( LuaException ) )]
		public void AccessInexistedMemberProperty()
		{
			const string LUA_CODE = @"
GameObject = wutLua.ImportType( 'UnityEngine.GameObject' )

local go = GameObject( 'go' )
local notExists = go.notExists
go.notExists = 123
			";
			Assert.Throws<LuaException>( () => _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) ) );
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
