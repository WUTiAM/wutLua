using UnityEditor.VersionControl;

namespace wutLua.Test
{
	using NUnit.Framework;
	using System.Text;
	using wutLua;

	[TestFixture]
	public class Test_LuaState_GetObject
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
			_luaState.DoBuffer( _LUA_CODE_BYTES );
		}

		[TearDown]
		public void PostTest()
		{
			_luaState.Dispose();
			_luaState = null;
		}

		[Test]
		public void GetObject_Premitive()
		{
			Assert.AreEqual( true, _luaState.GetObject( "gb" ) );
			Assert.AreEqual( null, _luaState.GetObject( "gb2" ) );

			Assert.AreEqual( 1234, _luaState.GetObject( "gn" ) );
			Assert.AreEqual( 1234.5678, _luaState.GetObject( "gn2" ) );
		}

		[Test]
		public void GetObject_String()
		{
			Assert.AreEqual( "xyz", _luaState.GetObject( "gs" ) );
			Assert.AreEqual( null, _luaState.GetObject( "gs2" ) );
		}

		[Test]
		public void GetObject_LuaTable()
		{
			LuaTable t = _luaState.GetObject( "gt" ) as LuaTable;

			Assert.AreEqual( null, t["b"] );
			Assert.AreEqual( 1234, t["n"] );
			Assert.AreEqual( "xyz", t["s"] );
		}

		[Test]
		public void GetObject_NestedPath()
		{
			Assert.AreEqual( true, _luaState.GetObject( "gt.t.b" ) );
			Assert.AreEqual( null, _luaState.GetObject( "gt.t.n" ) );
		}
	}
}
