using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace RoR2;

public class Inventory : NetworkBehaviour
{
	public delegate void EquipmentLostDelegate(in EquipmentState equipmentState, in EquipmentLocation location);

	public class EquipmentStateSlot
	{
		public EquipmentState[] _sets;

		public int _setCount;

		public byte activeSetIndex;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public EquipmentStateSlot(int setCount)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public ref EquipmentState GetEquipment(uint set)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void UpdateSetCount(int newSetCount)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void EnsureValidActiveSetIndex(int accessibleSetCount)
		{
			throw null;
		}
	}

	public struct EquipmentLocationQueryArgs
	{
		public enum SearchPriority : byte
		{
			None,
			EmptyFirst,
			OccupiedFirst
		}

		public static EquipmentLocationQueryArgs defaultPickup;

		public bool searchAllSlots;

		public bool searchCurrentSetOnly;

		public SearchPriority searchPriority;

		public bool allowEmpty;

		public bool allowOccupied;

		public bool allowInaccessible;

		public EquipmentMask equipmentWhitelist;

		[MethodImpl(MethodImplOptions.NoInlining)]
		static EquipmentLocationQueryArgs()
		{
			throw null;
		}
	}

	public readonly ref struct InventoryChangeScope
	{
		public Inventory inventory;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public InventoryChangeScope(Inventory inventory)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Dispose()
		{
			throw null;
		}
	}

	public struct GiveRandomItemsArgs
	{
		public int itemCountToGive;

		public float permanentWeight;

		public float temporaryWeight;

		public float rentalWeight;

		public bool isPickup;

		public bool giveLunarItems;

		public bool giveVoidItems;

		public bool giveFoodItems;

		public bool giveCursedItems;

		public ItemTier[] itemTiers;
	}

	public interface IChangeItemCountImpl
	{
		bool silent { get; }

		bool allowNegative { get; }

		int GetStackCount(ItemIndex itemIndex);

		void Add(ItemIndex itemIndex, int count);
	}

	public struct GiveItemPermanentImpl : IChangeItemCountImpl
	{
		public Inventory inventory;

		public bool silent
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			readonly get
			{
				throw null;
			}
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public bool allowNegative
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			get
			{
				throw null;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public readonly int GetStackCount(ItemIndex itemIndex)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Add(ItemIndex itemIndex, int count)
		{
			throw null;
		}
	}

	public struct GiveItemChanneledImpl : IChangeItemCountImpl
	{
		public Inventory inventory;

		public bool silent
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			get
			{
				throw null;
			}
		}

		public bool allowNegative
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			get
			{
				throw null;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public readonly int GetStackCount(ItemIndex itemIndex)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Add(ItemIndex itemIndex, int count)
		{
			throw null;
		}
	}

	public struct GiveItemRentedImpl : IChangeItemCountImpl
	{
		public Inventory inventory;

		public bool silent
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			get
			{
				throw null;
			}
		}

		public bool allowNegative
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
		public int GetStackCount(ItemIndex itemIndex)
		{
			throw null;
		}
	}

	public delegate void EquipmentChangedDetailedDelegate(in EquipmentChangedArgs args);

	public readonly struct EquipmentChangedArgs
	{
		public EquipmentIndex newEquipmentIndex;

		public EquipmentLocation newLocation;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public EquipmentChangedArgs(EquipmentIndex newEquipmentIndex, EquipmentLocation newLocation)
		{
			throw null;
		}
	}

	public struct CloneInventoryArgs
	{
		public static CloneInventoryArgs all;

		public Inventory src;

		public Func<ItemIndex, bool> canCloneFilter;

		public bool clonePermanentItems;

		public bool cloneTemporaryItems;

		public bool cloneRentalItems;

		public bool cloneEquipmentStates;

		public bool cloneKeyItem;

		public bool cloneItemAcquisitionOrder;

		public bool cloneItemDisablers;

		public bool cloneInventoryDisablers;

		public bool isValid
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			get
			{
				throw null;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static CloneInventoryArgs All(Inventory src)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CloneInventoryArgs CloneItems(bool clonePermanent, bool cloneTemporary, bool cloneRental)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CloneInventoryArgs CloneEquipment(bool value)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CloneInventoryArgs CloneKeyItem(bool value)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CloneInventoryArgs CloneItemAcquisitionOrder(bool value)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CloneInventoryArgs CloneItemDisablers(bool value)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CloneInventoryArgs CloneInventoryDisablers(bool value)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CloneInventoryArgs SetFilter(Func<ItemIndex, bool> filter)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static CloneInventoryArgs()
		{
			throw null;
		}
	}

	public struct CleanInventoryArgs
	{
		public static CleanInventoryArgs _all;

		public bool cleanEquipmentStates;

		public bool cleanKeyItem;

		public Func<ItemIndex, bool> canRemoveFilter;

		public bool cleanPermanentItemStacks;

		public bool cleanRentalItemStacks;

		public bool cleanTemporaryItemStacks;

		public bool cleanItemDisablers;

		public bool cleanInventoryDisablers;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static CleanInventoryArgs All()
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CleanInventoryArgs CleanItems(bool permanent, bool rental, bool temporary)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CleanInventoryArgs CleanEquipment(bool value)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CleanInventoryArgs CleanKeyItem(bool value)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CleanInventoryArgs SetFilter(Func<ItemIndex, bool> filter)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CleanInventoryArgs CleanItemDisablers(bool value)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CleanInventoryArgs CleanInventoryDisablers(bool value)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		static CleanInventoryArgs()
		{
			throw null;
		}
	}

	public struct ItemStackValues
	{
		public float temporaryStacksValue;

		public int permanentStacks;

		public int rentalStacks;

		public int totalStacks;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static ItemStackValues Create()
		{
			throw null;
		}
	}

	public struct ItemAndStackValues
	{
		public ItemIndex itemIndex;

		public ItemStackValues stackValues;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static ItemAndStackValues Create()
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public readonly ItemAndStackValues WithItemIndex(ItemIndex itemIndex)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public readonly int AddAsPickupsToList(List<UniquePickup> dest)
		{
			throw null;
		}
	}

	public struct TryTransformRandomItemArgs
	{
		public struct FilterArgs
		{
			public ItemIndex itemIndex;

			public ItemStorageType itemStorageType;
		}

		public delegate ItemIndex FilterDelegate(FilterArgs args);

		public Xoroshiro128Plus rng;

		public FilterDelegate filter;

		public bool forbidPermanent;

		public bool forbidTemporary;

		public bool forbidRental;
	}

	public struct TryTransformRandomItemsResult
	{
		public ItemIndex originalItemIndex;

		public ItemIndex newItemIndex;

		public ItemStorageType originalItemStorageType;
	}

	public struct ItemTransformation
	{
		public struct CanTakeResult
		{
			public Inventory inventory;

			public ItemAndStackValues takenItem;

			public ItemTransformationTypeIndex itemTransformationType;

			public bool doNotSendNotification;

			[MethodImpl(MethodImplOptions.NoInlining)]
			public TakeResult PerformTake()
			{
				throw null;
			}
		}

		public struct TakeResult
		{
			public Inventory inventory;

			public ItemAndStackValues takenItem;

			public ItemTransformationTypeIndex transformationType;

			public bool doNotSendNotification;

			[MethodImpl(MethodImplOptions.NoInlining)]
			public TryTransformResult GiveTakenItem(Inventory inventory, ItemIndex newItemIndex)
			{
				throw null;
			}
		}

		public struct TryTransformResult
		{
			public Inventory inventory;

			public ItemAndStackValues takenItem;

			public ItemAndStackValues givenItem;

			public ItemTransformationTypeIndex transformationType;

			public bool doNotSendNotification;

			public readonly int totalTransformed
			{
				[MethodImpl(MethodImplOptions.NoInlining)]
				get
				{
					throw null;
				}
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			public static TryTransformResult Create()
			{
				throw null;
			}
		}

		public int? _minToTransform;

		public int? _maxToTransform;

		public ItemTransformationTypeIndex transformationType;

		public ItemIndex originalItemIndex
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			readonly get
			{
				throw null;
			}
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public ItemIndex newItemIndex
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			readonly get
			{
				throw null;
			}
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public bool allowWhenDisabled
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			readonly get
			{
				throw null;
			}
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public int minToTransform
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			get
			{
				throw null;
			}
			[MethodImpl(MethodImplOptions.NoInlining)]
			set
			{
				throw null;
			}
		}

		public int maxToTransform
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			get
			{
				throw null;
			}
			[MethodImpl(MethodImplOptions.NoInlining)]
			set
			{
				throw null;
			}
		}

		public bool forbidRentItems
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			readonly get
			{
				throw null;
			}
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public bool forbidTempItems
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			readonly get
			{
				throw null;
			}
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public bool forbidPermanentItems
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			readonly get
			{
				throw null;
			}
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public bool countAsDiscoveringPickup
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			readonly get
			{
				throw null;
			}
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public bool doNotSendNotification
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			readonly get
			{
				throw null;
			}
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public static event Action<TryTransformResult> onItemTransformedServerGlobal
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			add
			{
				throw null;
			}
			[MethodImpl(MethodImplOptions.NoInlining)]
			[CompilerGenerated]
			remove
			{
				throw null;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public bool TryTransform(Inventory inventory, out TryTransformResult result)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public bool CanTake(Inventory inventory, out CanTakeResult result)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public CanTakeResult? CanTake(Inventory inventory)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public bool TryTake(Inventory inventory, out TakeResult result)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public TakeResult? TryTake(Inventory inventory)
		{
			throw null;
		}
	}

	[StructLayout(LayoutKind.Sequential, Size = 1)]
	public readonly struct RunFixedTimeStampNetSerializer : INetSerializer<Run.FixedTimeStamp>
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Serialize(NetworkWriter writer, in Run.FixedTimeStamp value)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Deserialize(NetworkReader reader, ref Run.FixedTimeStamp dest)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		void INetSerializer<Run.FixedTimeStamp>.Serialize(NetworkWriter writer, in Run.FixedTimeStamp value)
		{
			throw null;
		}
	}

	[StructLayout(LayoutKind.Sequential, Size = 1)]
	public readonly struct FixedTimeStampSparseListImpl : ISparseArrayImpl<Run.FixedTimeStamp>
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		public SparseIndex[] AllocDenseArray()
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public Run.FixedTimeStamp[] AllocSparseArray()
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void FreeDenseArray(SparseIndex[] array)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void FreeSparseArray(Run.FixedTimeStamp[] array)
		{
			throw null;
		}
	}

	public struct TempItemsStorage
	{
		public struct GiveItemTempImpl : IChangeItemCountImpl
		{
			public Inventory inventory;

			public bool silent
			{
				[MethodImpl(MethodImplOptions.NoInlining)]
				get
				{
					throw null;
				}
			}

			public bool allowNegative
			{
				[MethodImpl(MethodImplOptions.NoInlining)]
				get
				{
					throw null;
				}
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			public readonly int GetStackCount(ItemIndex itemIndex)
			{
				throw null;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			public void Add(ItemIndex itemIndex, int count)
			{
				throw null;
			}
		}

		public Inventory inventory;

		public SparseArrayStruct<Run.FixedTimeStamp, FixedTimeStampSparseListImpl> decayToZeroTimeStamps;

		public float decayDuration;

		public float invDecayDuration;

		public ItemCollection tempItemStacks;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public TempItemsStorage(Inventory inventory)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Dispose()
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void SyncStacksToDecay()
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void SyncStackToDecay(ItemIndex itemIndex)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void SetDecayDurationServer(float newDecayDuration)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public readonly float GetItemRawValue(ItemIndex index)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static float RawToStacksValue(float rawValue)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static float RawToDecayValue(float rawValue)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public readonly float GetItemDecayValue(ItemIndex index)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public readonly void WriteAllTempItemRawValues(Span<float> dest)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public readonly void WriteAllTempItemDecayValues(Span<float> values)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public readonly int GetTotalItemStacks()
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
		public float GiveItemTemp(ItemIndex itemIndex, float decayValue)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void ResetItem(ItemIndex itemIndex)
		{
			throw null;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public readonly int GetItemStacks(ItemIndex itemIndex)
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
	}

	public ItemCollection permanentItemStacks;

	public ItemCollection channeledItemStacks;

	public TempItemsStorage tempItemsStorage;

	public ItemCollection effectiveItemStacks;

	public ItemCollection totalItemStacks;

	public ItemCollection rentalItemStacks;

	public List<ItemIndex> itemAcquisitionOrder;

	public bool[] itemAcquisitionSet;

	public const uint itemListDirtyBit = 1u;

	public const uint infusionBonusDirtyBit = 2u;

	public const uint itemAcquisitionOrderDirtyBit = 4u;

	public const uint equipmentDirtyBit = 8u;

	public const uint tempItemsDirtyBit = 16u;

	public const uint channeledItemListDirtyBit = 32u;

	public const uint disabledDirtyBit = 64u;

	public const uint keyItemDirtyBit = 128u;

	public const uint rentedItemListDirtyBit = 256u;

	public const uint disabledItemMaskDirtyBit = 512u;

	public const uint allDirtyBits = 1023u;

	public const float baseItemDecayDuration = 80f;

	public byte[] _activeEquipmentSetBackingArray;

	public Run.FixedTimeStamp lastActiveEquipmentLocationChangedTimestamp;

	public bool wasRecentlyCreated;

	public EquipmentStateSlot[] _equipmentSlots;

	public bool equipmentDisabled;

	public KeyItemIndex _keyItemIndex;

	public int[] _disabledItemCounter;

	public ItemMask _disabledItems;

	public int inventoryDisablerCountServer;

	public bool _inventoryDisabled;

	public int inventoryChangeScopeCounter;

	public static Queue<Inventory> releaseQueue;

	[HideInInspector]
	public float beadAppliedHealth;

	[HideInInspector]
	public float beadAppliedShield;

	[HideInInspector]
	public float beadAppliedRegen;

	[HideInInspector]
	public float beadAppliedDamage;

	public static Func<ItemIndex, bool> defaultItemClearFilterDelegate;

	public static Func<ItemIndex, bool> defaultItemCopyFilterDelegate;

	public static int kCmdCmdSwitchToNextEquipmentInSet;

	public static int kCmdCmdSwitchToPreviousEquipmentInSet;

	public static int kRpcRpcTriggerInventoryChanged;

	public static int kRpcRpcItemAdded;

	public static int kRpcRpcClientEquipmentChanged;

	public static int kRpcRpcClientKeyItemChanged;

	public static int kRpcRpcDiscoverPickup;

	public EquipmentIndex currentEquipmentIndex
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	public EquipmentState currentEquipmentState
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	[Obsolete("This method's intent is unclear, use nextSlotEquipmentIndex or nextSetEquipmentIndex instead.")]
	public EquipmentIndex alternateEquipmentIndex
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	[Obsolete("This method's intent is unclear, use nextSlotEquipmentState or nextSetEquipmentState instead.")]
	public EquipmentState alternateEquipmentState
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	public EquipmentIndex nextSlotEquipmentIndex
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	public EquipmentState nextSlotEquipmentState
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	public EquipmentLocation nextSlotEquipmentLocation
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	public EquipmentIndex nextSetEquipmentIndex
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	public EquipmentState nextSetEquipmentState
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	public EquipmentLocation nextSetEquipmentLocation
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	public byte equipmentSlotCount
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	public byte equipmentSetCount
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		get
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		set
		{
			throw null;
		}
	}

	public byte accessibleEquipmentSetCount
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		get
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		set
		{
			throw null;
		}
	}

	public byte activeEquipmentSlot
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		get
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		set
		{
			throw null;
		}
	}

	public EquipmentLocation activeEquipmentLocation
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	[Obsolete("For Mod Compat Only. Use GetActiveEquipmentSetInSlot instead.", false)]
	public byte[] activeEquipmentSet
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	public bool wasActiveEquipmentLocationRecentlyChanged
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	public KeyItemDef keyItem
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	public KeyItemIndex keyItemIndex
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		set
		{
			throw null;
		}
	}

	public bool inventoryDisabled
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		set
		{
			throw null;
		}
	}

	public uint infusionBonus
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		get
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		set
		{
			throw null;
		}
	}

	public bool spawnedOverNetwork
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		get
		{
			throw null;
		}
	}

	public event Action onInventoryChanged
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		add
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		remove
		{
			throw null;
		}
	}

	public event Action onEquipmentExternalRestockServer
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		add
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		remove
		{
			throw null;
		}
	}

	public event EquipmentLostDelegate onEquipmentLost
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		add
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		remove
		{
			throw null;
		}
	}

	public event Action onActiveEquipmentLocationChanged
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		add
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		remove
		{
			throw null;
		}
	}

	public static event Action<Inventory> onInventoryChangedGlobal
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		add
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		remove
		{
			throw null;
		}
	}

	public static event Action<Inventory, ItemIndex, int> onServerItemGiven
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		add
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		remove
		{
			throw null;
		}
	}

	public event Action<ItemIndex> onItemAddedClient
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		add
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		remove
		{
			throw null;
		}
	}

	public event Action<EquipmentIndex, uint> onEquipmentChangedClient
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		add
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		remove
		{
			throw null;
		}
	}

	public event EquipmentChangedDetailedDelegate onEquipmentChangedDetailedClient
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		add
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		remove
		{
			throw null;
		}
	}

	public event Action<KeyItemIndex> onKeyItemAddedClient
	{
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		add
		{
			throw null;
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[CompilerGenerated]
		remove
		{
			throw null;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool HasEquipmentOverflow()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void UpdateEquipmentSlotCountInternal(byte newEquipmentSlotCount)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void RecalculateEquipmentSetsServer()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void UpdateEquipmentSetCountInternal(byte newEquipmentSetCount)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void UpdateAccessibleEquipmentSetCountInternal(byte newAccessibleEquipmentSetCount)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool TryGetEquipmentSlot(uint slotIndex, out EquipmentStateSlot equipmentSlot)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only", false)]
	public bool SetEquipmentInternal(EquipmentState equipmentState, uint slot)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only", false)]
	public bool SetEquipmentInternal(EquipmentState equipmentState, uint slot, uint set)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool SetEquipmentInternal(in EquipmentState equipmentState, in EquipmentLocation location)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool IsEquipmentLocationInBounds(in EquipmentLocation location)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool CheckEquipmentLocationInBounds(in EquipmentLocation location)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool EquipmentSetFull()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void DispatchSwitchToNextEquipmentInSet()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Command]
	public void CmdSwitchToNextEquipmentInSet()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void SwitchToNextEquipmentInSet()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Command]
	public void CmdSwitchToPreviousEquipmentInSet()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void SwitchToPreviousEquipmentInSet()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. This method's intent is unclear, query a desired equipment location and retrieve an EquipmentIndex from that instead.", false)]
	public EquipmentIndex GetReplacedEquipmentIndex()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"SetEquipment(EquipmentState equipmentState, uint slot, uint set)\" instead.", false)]
	[Server]
	public void SetEquipment(EquipmentState equipmentState, uint slot)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"SetEquipment(EquipmentState equipmentState, EquipmentLocation location)\" instead.", false)]
	[Server]
	public void SetEquipment(EquipmentState equipmentState, uint slot, uint set)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void SetEquipment(in EquipmentState equipmentState, in EquipmentLocation location)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public EquipmentState GetActiveEquipment()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"GetEquipment(EquipmentLocation location)\" instead.", false)]
	public EquipmentState GetEquipment(uint slot)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"GetEquipment(EquipmentLocation location)\" instead.", false)]
	public EquipmentState GetEquipment(uint slot, uint set)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public EquipmentState GetEquipment(in EquipmentLocation location)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void SetActiveEquipmentSlot(byte slotIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void SetActiveEquipmentSet(byte setIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public bool TrySetActiveEquipmentLocation(in EquipmentLocation location)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void HandleActiveEquipmentLocationChanged()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. No longer in use.", false)]
	public void AddEquipmentSet()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. No longer in use.", false)]
	public void RemoveEquipmentSet()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void SetEquipmentDisabled(bool _active)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool GetEquipmentDisabled()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetEquipmentSlotCount()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetEquipmentSetCount()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public uint GetActiveEquipmentSetInSlot(uint slot)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Equipment set count is the same across all slots.", false)]
	public int GetEquipmentSetCount(uint slot)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("Use TryFindBestEquipmentLocation instead", false)]
	public uint FindBestEquipmentSetIndex()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("Use TryFindBestEquipmentLocation instead", false)]
	public uint FindBestEquipmentSetIndex(bool findExisting = false)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool TryFindBestEquipmentLocation(in EquipmentLocationQueryArgs args, out EquipmentLocation location)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public EquipmentLocation FindBestEquipmentLocationOrCurrent(in EquipmentLocationQueryArgs args)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"SetEquipmentIndex(EquipmentIndex newEquipmentIndex, EquipmentLocation location)\" instead.", false)]
	[Server]
	public void SetEquipmentIndex(EquipmentIndex newEquipmentIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"SetEquipmentIndex(EquipmentIndex newEquipmentIndex, EquipmentLocation location)\" instead.", false)]
	[Server]
	public void SetEquipmentIndex(EquipmentIndex newEquipmentIndex, bool isRemovingEquipment)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"SetEquipmentIndex(EquipmentIndex newEquipmentIndex, EquipmentLocation location)\" instead.", false)]
	[Server]
	public void SetEquipmentIndexForSlot(EquipmentIndex newEquipmentIndex, uint slot)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"SetEquipmentIndex(EquipmentIndex newEquipmentIndex, EquipmentLocation location)\" instead.", false)]
	[Server]
	public void SetEquipmentIndexForSlot(EquipmentIndex newEquipmentIndex, uint slot, uint set)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void SetEquipmentIndex(EquipmentIndex newEquipmentIndex, in EquipmentLocation location)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public EquipmentIndex GetEquipmentIndex()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public EquipmentIndex GetEquipmentIndex(in EquipmentLocation location)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	[Obsolete("For Mod Compat Only. Use \"DeductEquipmentCharges(EquipmentLocation location, int deduction)\" instead.", false)]
	public void DeductEquipmentCharges(byte slot, int deduction)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"DeductEquipmentCharges(EquipmentLocation location, int deduction)\" instead.", false)]
	[Server]
	public void DeductEquipmentCharges(byte slot, byte set, int deduction)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void DeductEquipmentCharges(in EquipmentLocation location, int deduction)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetActiveEquipmentRestockableChargeCount()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"GetEquipmentRestockableChargeCount(EquipmentLocation location)\" instead.", false)]
	public int GetEquipmentRestockableChargeCount(byte slot)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"GetEquipmentRestockableChargeCount(EquipmentLocation location)\" instead.", false)]
	public int GetEquipmentRestockableChargeCount(byte slot, byte set)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetEquipmentRestockableChargeCount(in EquipmentLocation location)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void RestockActiveEquipmentCharges(int amount)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"RestockEquipmentCharges(EquipmentLocation location, int amount)\" instead.", false)]
	[Server]
	public void RestockEquipmentCharges(byte slot, int amount)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"RestockEquipmentCharges(EquipmentLocation location, int amount)\" instead.", false)]
	[Server]
	public void RestockEquipmentCharges(byte slot, byte set, int amount)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void RestockEquipmentCharges(in EquipmentLocation location, int amount)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void DeductActiveEquipmentCooldown(float seconds)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"DeductEquipmentCooldown(EquipmentLocation location, float percentage)\" instead.", false)]
	[Server]
	public void DeductEquipmentCooldown(byte slot, float percentage)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	[Obsolete("For Mod Compat Only. Use \"DeductEquipmentCooldown(EquipmentLocation location, float percentage)\" instead.", false)]
	public void DeductEquipmentCooldown(byte slot, byte set, float percentage)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void DeductEquipmentCooldown(in EquipmentLocation location, float percentage)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. Use \"GetEquipmentSlotMaxCharges()\" instead.", false)]
	public int GetEquipmentSlotMaxCharges(byte slot)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetEquipmentSlotMaxCharges()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetActiveEquipmentMaxCharges()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void UpdateEquipment()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public float CalculateEquipmentCooldownScale()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool SetKeyItemInternal(KeyItemIndex keyItemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void SetKeyItem(KeyItemIndex keyItemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public KeyItemIndex GetKeyItemIndex()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public bool RemoveItemDisabler(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void AddItemDisabler(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool IsItemDisabled(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[ClientRpc]
	public void RpcTriggerInventoryChanged()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void AddInventoryDisabler()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void RemoveInventoryDisabler()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void HandleInventoryChanged()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[CompilableComment("In case you need to do a complex change to the inventory, you can utilize this method to ensure \"HandleInventoryChanged\" is called once, instead of multiple times.")]
	public void InventoryChangedHandler(Action<Inventory> inventoryModification)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void OnEnable()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void OnDisable()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void OnSceneChange(Scene previous, Scene current)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void Awake()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void OnDestroy()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void FixedUpdate()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	static Inventory()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void StaticFixedUpdate()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void AcquirePooledResources()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void ReleasePooledResources()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void MyFixedUpdate(float deltaTime)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void RefreshInventoryStateServer()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void AddInfusionBonus(uint value)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void GiveItemString(string itemString)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void GiveItemString(string itemString, int count)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void GiveEquipmentString(string equipmentString)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void GiveRandomItems(int count, bool lunarEnabled, bool voidEnabled)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void GiveRandomItems(int count, params ItemTier[] tiers)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void GiveRandomItems(GiveRandomItemsArgs args)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void GiveRandomEquipment()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void GiveRandomEquipment(Xoroshiro128Plus rng)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void SetItemAcquiredServer(ItemIndex itemIndex, bool acquired)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("Utilize \"GetItemCountTotal\"instead.", false)]
	public int CalculateEffectiveItemStacks(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void UpdateAllEffectiveItemStacks()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void UpdateEffectiveItemStacks(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void ChangeItemStacksCount<TImpl>(TImpl impl, ItemIndex itemIndex, int countToAdd) where TImpl : struct, IChangeItemCountImpl
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("Use .GiveItemPermanent instead.", false)]
	[Server]
	public void GiveItem(ItemIndex itemIndex, int countToAdd = 1)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void GiveItemPermanent(ItemIndex itemIndex, int countToAdd = 1)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void GiveItemChanneled(ItemIndex itemIndex, int countToAdd = 1)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void GiveItemTemp(ItemIndex itemIndex, float countToAdd = 1f)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void GiveItemRented(ItemIndex itemIndex, int countToAdd = 1)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("Use .GiveItemPermanent instead.", false)]
	[Server]
	public void GiveItem(ItemDef itemDef, int count = 1)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void GiveItemPermanent(ItemDef itemDef, int count = 1)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void GiveKeyItem(KeyItemIndex keyItemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[ClientRpc]
	public void RpcItemAdded(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[ClientRpc]
	public void RpcClientEquipmentChanged(EquipmentIndex newEquipIndex, EquipmentLocation location)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[ClientRpc]
	public void RpcClientKeyItemChanged(KeyItemIndex newKeyItemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void RemoveEquipment(EquipmentIndex equipmentIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool HasEquipment(EquipmentIndex equipmentIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	[Obsolete("Use .RemoveItemPermanent instead.", false)]
	public void RemoveItem(ItemIndex itemIndex, int count = 1)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void RemoveItemPermanent(ItemIndex itemIndex, int count = 1)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void RemoveItemTemp(ItemIndex itemIndex, float count = 1f)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void RemoveKeyItem(KeyItemIndex keyItemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void RemoveItemChanneled(ItemIndex itemIndex, int count = 1)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void RemoveItemRented(ItemIndex itemIndex, int count = 1)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	[Obsolete("Use .RemoveItemPermanent instead.", false)]
	public void RemoveItem2(ItemDef itemDef, int count = 1)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void RemoveItemPermanent(ItemDef itemDef, int count = 1)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("Use .ResetItemPermanent instead.", false)]
	[Server]
	public void ResetItem(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void ResetItemPermanent(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("Use .ResetItemPermanent instead.", false)]
	[Server]
	public void ResetItem(ItemDef itemDef)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void ResetItemPermanent(ItemDef itemDef)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void ResetItemTemp(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void ResetItemRented(ItemDef itemDef)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void ResetItemRented(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	[Obsolete("Use CopyEquipmentFrom(Inventory other, bool includeChargeData) going forward.")]
	public void CopyEquipmentFrom(Inventory other)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void CopyEquipmentFrom(Inventory other, bool includeChargeData)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void CopyEquipmentFrom(EquipmentMap<EquipmentState> src, bool includeChargeData)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void CopyEquipmentFrom(EquipmentMap<EquipmentIndex> src)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void GetEquipmentMap(EquipmentMap<EquipmentState> dest, bool includeChargeData)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public EquipmentMap<EquipmentState> GetEquipmentMap(bool includeChargeData)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void GetEquipmentIndicesMap(EquipmentMap<EquipmentIndex> dest)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public EquipmentMap<EquipmentIndex> GetEquipmentIndicesMap()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("For Mod Compat Only. No longer in use.", false)]
	public void FixExtraEquipmentCounts()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static bool DefaultItemCopyFilter(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static bool DefaultItemClearFilter(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	[Obsolete("This method should not be used, utilize AddItemsFrom or CloneInventory instead.")]
	public void AddItemsFrom([NotNull] int[] otherItemStacks, [NotNull] Func<ItemIndex, bool> filter)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void AddItemsFrom([NotNull] Inventory other)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void AddItemAcquisitionOrderFrom([NotNull] List<ItemIndex> otherItemAcquisitionOrder)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void CloneItemInventory([NotNull] Inventory other)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void CopyItemsFrom([NotNull] Inventory other)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void CopyItemsFrom([NotNull] Inventory other, [NotNull] Func<ItemIndex, bool> filter)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void AddItemsFrom([NotNull] Inventory other, [NotNull] Func<ItemIndex, bool> filter)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void CloneInventory(CloneInventoryArgs args)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void CleanInventory()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void CleanInventory(CleanInventoryArgs args)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void ShrineRestackInventory([NotNull] Xoroshiro128Plus rng)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[CompilableComment("With the introduction of item disabling and temp items, GetItemCount had to redirect to either the behavior of .GetItemCountEffective (for item disabling, temp items, etc.) or .GetItemCountPermanent (for things like consumable items or purchases). Redirection to .GetItemCountEffective was chosen as it's expected to affect far fewer mods than the alternative. GiveItem and RemoveItem have been redirected to the permanent versions, as it's the only option that makes sense.")]
	[Obsolete("Use .GetItemCountEffective or .GetItemCountPermanent instead.", false)]
	public int GetItemCount(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("Use .GetItemCountEffective or .GetItemCountPermanent instead.", false)]
	public int GetItemCount(ItemDef itemDef)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public float GetItemDurationPercent(int itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetItemCountEffective(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetItemCountEffective(ItemDef itemDef)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[CompilableComment("This method is used to get the Effective Item Stacks without taking into consideration Item Disabling and Inventory Disabling.")]
	public int GetItemCountTotal(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[CompilableComment("This method is used to get the Effective Item Stacks without taking into consideration Item Disabling and Inventory Disabling.")]
	public int GetItemCountTotal(ItemDef itemDef)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetItemCountPermanent(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetItemCountPermanent(ItemDef itemDef)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetItemCountChanneled(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetItemCountChanneled(ItemDef itemDef)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetItemCountRented(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetItemCountRented(ItemDef itemDef)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetItemCountTemp(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetItemCountTemp(ItemDef itemDef)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public float GetTempItemDecayValue(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public float GetTempItemRawValue(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetStorageItemCount(ItemDef itemDef, ItemStorageType itemStorageType)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetStorageItemCount(ItemIndex itemIndex, ItemStorageType itemStorageType)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetTotalItemCount()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetTotalTempItemCount()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool HasAtLeastXTotalRemovablePermanentItemsOfTier(ItemTier itemTier, int x)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public bool HasAtLeastXTotalRemovablePermanentItemsOfTier(ItemTier itemTier, int amount, ItemMask blacklistedMask)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Obsolete("Use HasAtLeastXTotalRemovablePermanentItemsOfTier instead.", false)]
	public bool HasAtLeastXTotalItemsOfTier(ItemTier itemTier, int x)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public int GetTotalItemCountOfTier(ItemTier itemTier)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void WriteItemStacks(int[] output)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void WriteAllPermanentItemStacks(Span<int> dest)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void WriteAllTempItemDecayValues(Span<float> dest)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void WriteAllRentalItemStacks(Span<int> dest)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void WriteAllTempItemRawValues(Span<float> dest)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void SetItemDecayDurationServer(float duration)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void WriteDisabledItemMask(ItemMask output)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public override int GetNetworkChannel()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public override void OnDeserialize(NetworkReader reader, bool initialState)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public override bool OnSerialize(NetworkWriter writer, bool initialState)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public bool TryTransformRandomItem(TryTransformRandomItemArgs args, out TryTransformRandomItemsResult result)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void EnforceItemStackLimit(ItemIndex itemIndex, int maxStacks)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void ThrowIfInvalid()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[Server]
	public void DiscoverPickup(PickupIndex pickupIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	[ClientRpc]
	public void RpcDiscoverPickup(PickupIndex pickupIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public Inventory()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void UNetVersion()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void InvokeCmdCmdSwitchToNextEquipmentInSet(NetworkBehaviour obj, NetworkReader reader)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void InvokeCmdCmdSwitchToPreviousEquipmentInSet(NetworkBehaviour obj, NetworkReader reader)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void CallCmdSwitchToNextEquipmentInSet()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void CallCmdSwitchToPreviousEquipmentInSet()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void InvokeRpcRpcTriggerInventoryChanged(NetworkBehaviour obj, NetworkReader reader)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void InvokeRpcRpcItemAdded(NetworkBehaviour obj, NetworkReader reader)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void InvokeRpcRpcClientEquipmentChanged(NetworkBehaviour obj, NetworkReader reader)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void InvokeRpcRpcClientKeyItemChanged(NetworkBehaviour obj, NetworkReader reader)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void InvokeRpcRpcDiscoverPickup(NetworkBehaviour obj, NetworkReader reader)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void CallRpcTriggerInventoryChanged()
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void CallRpcItemAdded(ItemIndex itemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void CallRpcClientEquipmentChanged(EquipmentIndex newEquipIndex, EquipmentLocation location)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void CallRpcClientKeyItemChanged(KeyItemIndex newKeyItemIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void CallRpcDiscoverPickup(PickupIndex pickupIndex)
	{
		throw null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public override void PreStartClient()
	{
		throw null;
	}
}
You are not using the latest version of the tool, please update.
Latest version is '11.2.0.10050' (yours is '9.0.0.7889')
