namespace wutLua.Test
{
	using NUnit.Framework;
	using System.Text;
	using wutLua;

	[TestFixture]
	public class Test_LuaState
	{
		const string _LUA_CODE = @"
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
		readonly byte[] _LUA_CODE_BYTES = Encoding.UTF8.GetBytes( _LUA_CODE );

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
		public void GetObject()
		{
			_luaState.DoBuffer( _LUA_CODE_BYTES );

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
			_luaState.SetObject( "gb", false );
			_luaState.DoBuffer( _LUA_CODE_BYTES );

			Assert.AreEqual( true, _luaState.GetObject( "gb" ) );
			Assert.AreEqual( true, _luaState.GetObject( "gt.t.b" ) );
			Assert.AreEqual( null, _luaState.GetObject( "gt.t.s" ) );

			_luaState.SetObject( "gb", false );
			_luaState.SetObject( "gt.t.s", "abc" );
			_luaState.SetObject( "gt.t2.s", "abc" );

			Assert.AreEqual( false, _luaState.GetObject( "gb" ) );
			Assert.AreEqual( true, _luaState.GetObject( "gt.t.b" ) );
			Assert.AreEqual( "abc", _luaState.GetObject( "gt.t.s" ) );
			Assert.AreEqual( null, _luaState.GetObject( "gt.t2.s" ) );
		}

		[Test]
		public void ToObject()
		{
		}
	}
}
