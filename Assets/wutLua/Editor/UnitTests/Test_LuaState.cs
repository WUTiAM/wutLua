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
			Assert.AreEqual( "abc", _luaState.GetObject( "gs" ) );
		}

		[Test]
		public void GetObject()
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
			Assert.AreEqual( true, _luaState.GetObject( "gb" ) );
			Assert.AreEqual( null, _luaState.GetObject( "gb2" ) );

			// Get number
			Assert.AreEqual( 4321, _luaState.GetObject( "gn" ) );
			Assert.AreEqual( 1234.5678, _luaState.GetObject( "gn2" ) );

			// Get string
			Assert.AreEqual( "zyx", _luaState.GetObject( "gs" ) );
			Assert.AreEqual( null, _luaState.GetObject( "gs2" ) );

			// Get table and access elements
			LuaTable t = _luaState.GetObject( "gt" ) as LuaTable;
			Assert.AreEqual( null, t["b"] );
			Assert.AreEqual( 1234, t["n"] );
			Assert.AreEqual( "xyz", t["s"] );

			// Get nested table element
			Assert.AreEqual( true, _luaState.GetObject( "gt.t.b" ) );
			Assert.AreEqual( null, _luaState.GetObject( "gt.t.n" ) );
			Assert.AreEqual( null, _luaState.GetObject( "gt.t2.b" ) );
			Assert.AreEqual( null, _luaState.GetObject( "gt2.t.b" ) );
		}

		[Test]
		public void PushObject()
		{
		}

		[Test]
		public void SetObject()
		{
			const string LUA_CODE = @"
gt = {
	t = {
		b = gb,
	},
}
			";
			_luaState.SetObject( "gb", true );
			_luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			_luaState.SetObject( "gb", false );
			_luaState.SetObject( "gt.t.s", "abc" );
			_luaState.SetObject( "gt.t2.s", "abc" );

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

		[Test]
		public void ToObject()
		{
		}
	}
}
