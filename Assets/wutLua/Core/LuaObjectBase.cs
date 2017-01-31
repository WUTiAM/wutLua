namespace wutLua
{
	using System;

	public abstract class LuaObjectBase : IDisposable
	{
		protected LuaState _LuaState { get; private set; }
		protected int _Reference { get; set; }

		bool _isDisposed;

		protected LuaObjectBase( LuaState luaState, int reference = LuaReferences.LUA_REFNIL )
		{
			_LuaState = luaState;
			_Reference = reference;
		}

#region Object members
		public override bool Equals( object o )
		{
			LuaObjectBase luaObject = o as LuaObjectBase;

			return luaObject != null && _Reference == luaObject._Reference;
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
			return _Reference;
		}
#endregion

		public void Push()
		{
			LuaLib.lua_rawgeti( _LuaState.L, LuaIndices.LUA_REGISTRYINDEX, _Reference );	// |o
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
				if( _Reference != 0 && _LuaState.L != IntPtr.Zero )
				{
					LuaLib.luaL_unref( _LuaState.L, LuaIndices.LUA_REGISTRYINDEX, _Reference );
				}
			}

			_LuaState = null;

			_isDisposed = true;
		}
	}
}
