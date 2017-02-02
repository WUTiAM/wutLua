namespace wutLua.Test
{
	using NUnit.Framework;
	using System.Text;
	using wutLua;

	[TestFixture]
	public class Test_LuaState_PushObject
	{
		const string _LUA_CODE = @"
gb = true
gn = 1234
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
		public void PushObject_Bool()
		{
		}
	}
}
