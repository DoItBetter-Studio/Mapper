namespace Glyphborn.Mapper.Tiles
{
	public enum CollisionType : byte
	{
		None		= 0x00, // Walkable
		Solid		= 0x01, // Wall / cliff
		Water		= 0x02, // Surf / swim
		Ledge		= 0x03, // One-way drop
		Stairs		= 0x04, // Height transition
		TallGrass	= 0x05, // Encounter trigger
		Door		= 0x06, // Map transition
		Script		= 0x07, // Event-controlled
	}
}
