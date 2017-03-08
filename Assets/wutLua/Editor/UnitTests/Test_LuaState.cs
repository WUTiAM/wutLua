namespace wutLua.Test
{
	using NUnit.Framework;
	using System.Text;
	using wutLua;

	[TestFixture]
	public class Test_LuaState
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
		public void DoBuffer()
		{
			const string LUA_CODE = @"
gs = 'abc'
local n = 123
local b = true

return nil, n, b
			";
			object[] results = _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			Assert.AreEqual( 3, results.Length );
			Assert.AreEqual( null, results[0] );
			Assert.AreEqual( 123, results[1] );
			Assert.AreEqual( true, results[2] );
			Assert.AreEqual( "abc", _luaState.Get( "gs" ) );
		}

		[Test]
		public void Get()
		{
			const string LUA_CODE = @"
gb = true
gn = 1234
gn2 = 1234.5678
gs = 'xyz'
gt = {
	n = gn,
	s = gs,
	t = {
		b = gb,
	},
}

gn = 4321
gs = 'zyx'
			";
			_luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			// Get bool
			Assert.AreEqual( true, _luaState.Get( "gb" ) );
			Assert.AreEqual( null, _luaState.Get( "gb2" ) );

			// Get number
			Assert.AreEqual( 4321, _luaState.Get( "gn" ) );
			Assert.AreEqual( 1234.5678, _luaState.Get( "gn2" ) );

			// Get string
			Assert.AreEqual( "zyx", _luaState.Get( "gs" ) );
			Assert.AreEqual( null, _luaState.Get( "gs2" ) );

			// Get table and access elements
			LuaTable t = _luaState.Get( "gt" ) as LuaTable;
			Assert.AreEqual( null, t["b"] );
			Assert.AreEqual( 1234, t["n"] );
			Assert.AreEqual( "xyz", t["s"] );

			// Get nested table element
			Assert.AreEqual( true, _luaState.Get( "gt.t.b" ) );
			Assert.AreEqual( null, _luaState.Get( "gt.t.n" ) );
			Assert.AreEqual( null, _luaState.Get( "gt.t2.b" ) );
			Assert.AreEqual( null, _luaState.Get( "gt2.t.b" ) );
		}

		[Test]
		public void Set()
		{
			const string LUA_CODE = @"
gt = {
	t = {
		b = gb,
	},
}
			";
			_luaState.Set( "gb", true );
			_luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			_luaState.Set( "gb", false );
			_luaState.Set( "gt.t.s", "abc" );
			_luaState.Set( "gt.t2.s", "abc" );

			const string LUA_CHECK_CODE = @"
if gb ~= false then
	return -1
end
if gt.t.b ~= true then
	return -111
end
if gt.t.s ~= 'abc' then
	return -112
end
if gt.t2 ~= nil then
	return -121
end

return 0
			";
			object[] results = _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CHECK_CODE ) );

			Assert.AreEqual( 1, results.Length );
			Assert.AreEqual( 0, results[0] );
		}

//		[Test]
//		public void ToObject()
//		{
//		}
//
//		[Test]
//		public void PushObject()
//		{
//		}

		[Test]
		public void PushCSObject()
		{
			const string LUA_CODE = @"
Camera = wutLua.ImportType( 'UnityEngine.Camera' )
GameObject = wutLua.ImportType( 'UnityEngine.GameObject' )

local cameraGO = GameObject( 'Camera', Camera )
local cameraComponent = cameraGO:GetComponent( Camera )
local cameraComponent2 = cameraGO:GetComponent( 'Camera' )

return cameraComponent, cameraComponent2, cameraComponent == cameraComponent2
			";
			object[] results = _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			Assert.AreEqual( 3, results.Length );
			Assert.AreSame( results[0], results[1] );
			Assert.AreEqual( true, results[2] );
		}
	}
}
