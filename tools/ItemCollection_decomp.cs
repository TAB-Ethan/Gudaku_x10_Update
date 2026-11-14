using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine.Networking;

namespace RoR2;

public struct ItemCollection
{
	[StructLayout(LayoutKind.Sequential, Size = 1)]
	public readonly struct IntNetSerializer : INetSerializer<int>
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Serialize(NetworkWriter writer, in int value)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Deserialize(NetworkReader reader, ref int dest)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		void INetSerializer<int>.Serialize(NetworkWriter writer, in int value)
		{
			throw null;
		}
	}

	[StructLayout(LayoutKind.Sequential, Size = 1)]
	public struct SparseArrayImpl : ISparseArrayImpl<int>
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public SparseIndex[] AllocDenseArray()
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public int[] AllocSparseArray()
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void FreeDenseArray(SparseIndex[] array)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void FreeSparseArray(int[] array)
		{
			throw null;
		}
	}

	public SparseArrayStruct<int, SparseArrayImpl> inner;

	public readonly bool isValid
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void Add(ItemIndex itemIndex, int count)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public readonly void GetNonZeroIndices(List<ItemIndex> dest)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public readonly ReadOnlySpan<ItemIndex> GetNonZeroIndicesSpan()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public readonly int GetStackValue(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void SetStackValue(ItemIndex itemIndex, int stackCount)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public readonly int GetTotalItemStacks()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public readonly void AddTo(Span<int> dest)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void Clear()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public readonly void Serialize(NetworkWriter writer)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void Deserialize(NetworkReader reader)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ItemCollection Create()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void Dispose()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public readonly bool HasAtLeastXTotalRemovableItemsOfTier(ItemTier itemTier, int x)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public readonly bool HasAtLeastXTotalRemovableItemsOfTier(ItemTier itemTier, int amountRequired, ItemMask blacklistedMask)
	{
		throw null;
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050' (yours is '9.0.0.7889')
