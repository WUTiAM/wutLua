namespace wutLua
{
	using System;

	public abstract class LuaObjectBase : IDisposable
	{
		protected LuaState _LuaState { get; private set; }
		protected int _RefId { get; set; }

		bool _isDisposed;

		protected LuaObjectBase( LuaState luaState, int refId = LuaReferences.LUA_REFNIL )
		{
			_LuaState = luaState;
			_RefId = refId;
		}

#region Object members
		public override bool Equals( object o )
		{
			LuaObjectBase luaObject = o as LuaObjectBase;

			return luaObject != null && _RefId == luaObject._RefId;
		}

		public static bool operator ==( LuaObjectBase a, LuaObjectBase b )
		{
			return Equals( a, b );
		}

		public static bool operator !=( LuaObjectBase a, LuaObjectBase b )
		{
			return !Equals( a, b );
		}

		public override int GetHashCode()
		{
			return _RefId;
		}
#endregion

		public void Push()
		{
			LuaLib.lua_rawgeti( _LuaState.L, LuaIndices.LUA_REGISTRYINDEX, _RefId );	// |o
		}

		~LuaObjectBase()
		{
			Dispose( false );
		}

#region IDisposable members
		public void Dispose()
		{
			Dispose( true );
			GC.SuppressFinalize( this );
		}
#endregion

		public virtual void Dispose( bool disposeManagedResources )
		{
			if( _isDisposed )
				return;

			if( disposeManagedResources )
			{
				if( _RefId != 0 && _LuaState.L != IntPtr.Zero )
				{
					LuaLib.luaL_unref( _LuaState.L, LuaIndices.LUA_REGISTRYINDEX, _RefId );
				}
			}

			_LuaState = null;

			_isDisposed = true;
		}
	}
}
