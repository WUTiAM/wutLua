using UnityEditor;

namespace wutLua.Test
{
	using NUnit.Framework;
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
		public void LuaAccessConstructor()
		{
			const string LUA_CODE = @"
wutLua.ImportType( 'UnityEngine.GameObject' )

local go = UnityEngine.GameObject()
if go == nil then
	return -1
end

return 0
			";
			object[] results = _luaState.DoBuffer( Encoding.UTF8.GetBytes( LUA_CODE ) );

			Assert.AreEqual( 1, results.Length );
			Assert.AreEqual( 0, (int)(double) results[0] );
		}
	}
}
