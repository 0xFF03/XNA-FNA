using System;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyGame.Engine.Core;
using MyGame.Engine.Platform;
using MyGame.Engine.StandardModules.Multiplayer;
using MyGame.Engine.StandardModules.Physics2D;
using MyGame.Game.Core;
using nkast.Aether.Physics2D.Dynamics;

using FlecsWorld = Flecs.NET.Core.World;
using AetherVector2 = nkast.Aether.Physics2D.Common.Vector2;

namespace MyGame.Game.Logic;

public static class MapEntityFactory
{
    public static void BuildEntity(FlecsWorld ecsWorld, LdtkEntityInstance interactable, string activeDimension, ulong netId, ulong hostId)
    {
        if (interactable.Identifier == "ShipExterior" || interactable.Identifier == "Cruiser")
        {
            BuildShipVehicle(ecsWorld, interactable, activeDimension, netId, hostId);
        }
        else
        {
            BuildGenericInteractable(ecsWorld, interactable, activeDimension, netId, hostId);
        }
    }

    private static void BuildShipVehicle(FlecsWorld ecsWorld, LdtkEntityInstance interactable, string activeDimension, ulong netId, ulong hostId)
    {
        float entWidth = interactable.Width > 0 ? interactable.Width : 32f;
        float entHeight = interactable.Height > 0 ? interactable.Height : 32f;
        float centerX = interactable.Px[0] + (entWidth / 2f);
        float centerY = interactable.Px[1] + (entHeight / 2f);

        int savedInteractionState = GetSavedState(interactable.Iid, ref centerX, ref centerY);

        string textureName = $"Textures/{interactable.Identifier}.png";
        string interiorDim = "Ship_Interior";
        Vector2 doorOffset = new Vector2(0, entHeight / 2f);

        if (interactable.FieldInstances != null)
        {
            var texField = interactable.FieldInstances.FirstOrDefault(f => f.Identifier.Equals("Texture", StringComparison.OrdinalIgnoreCase));
            if (texField != null && texField.Value != null) textureName = $"Textures/{texField.Value}.png";

            var targetDimField = interactable.FieldInstances.FirstOrDefault(f => f.Identifier.Equals("TargetDimension", StringComparison.OrdinalIgnoreCase));
            if (targetDimField != null && targetDimField.Value != null) interiorDim = targetDimField.Value.ToString()!;

            var offsetField = interactable.FieldInstances.FirstOrDefault(f => f.Identifier.Equals("DoorOffset", StringComparison.OrdinalIgnoreCase));
            if (offsetField != null && offsetField.Value != null && offsetField.Value is JsonElement offsetJe)
            {
                if (offsetJe.TryGetProperty("cx", out JsonElement cxProp) && offsetJe.TryGetProperty("cy", out JsonElement cyProp))
                {
                    float absoluteDoorX = (cxProp.GetSingle() * 16f) + 8f;
                    float absoluteDoorY = (cyProp.GetSingle() * 16f) + 8f;
                    doorOffset = new Vector2(absoluteDoorX - centerX, absoluteDoorY - centerY);
                }
            }
        }

        Texture2D shipTex = AssetManager.GetTexture(textureName);
        float physW = shipTex != AssetManager.WhitePixel ? shipTex.Width : 64f;
        float physH = shipTex != AssetManager.WhitePixel ? shipTex.Height : 64f;

        string uniqueInstanceInteriorName = $"{interiorDim}_{netId}";

        var vehicle = ecsWorld.Entity()
            .Add<TopDownTag>()
            .Add<MatchEntityTag>()
            .Add<InteractableTag>()
            .Add<RotationalDriveTag>()
            .Set(new Position { X = centerX, Y = centerY, Rotation = 0f })
            .Set(new PreviousPosition { X = centerX, Y = centerY, Rotation = 0f })
            .Set(new Velocity { X = 0, Y = 0 })
            .Set(new MovementCapabilities { MoveSpeed = 16f, JumpForce = 0 })
            .Set(new ShipEngine { CurrentThrust = 0f })
            .Set(new VehicleFlightState { TargetFlying = false, AltitudeRatio = 0f }) // ARCHITECTURE FIX: Begins firmly planted on the ground
            .Set(new ShipVehicleComponent { TextureName = textureName, DoorLocalOffset = doorOffset, InteriorDimensionName = uniqueInstanceInteriorName })
            .Set(new PhysicsDimension { Name = activeDimension })
            .Set(new NetworkId { Value = netId })
            .Set(new NetworkOwner { Value = hostId })
            .Set(new WorldMark { UniqueMarkId = interactable.Iid, InteractionState = savedInteractionState });

        var physicsWorld = PhysicsWorldManager.GetWorld(activeDimension);
        var initialPos = new AetherVector2(centerX / PhysicsSettings.PixelsPerMeter, centerY / PhysicsSettings.PixelsPerMeter);

        var aetherBody = physicsWorld.CreateRectangle(physW / PhysicsSettings.PixelsPerMeter, physH / PhysicsSettings.PixelsPerMeter, 1f, initialPos, 0f, BodyType.Dynamic);
        aetherBody.FixedRotation = false;

        // ARCHITECTURE FIX: Massively increased Damping to fix the "Lightspeed ice-skate" physics
        aetherBody.LinearDamping = 3.0f;
        aetherBody.AngularDamping = 5.0f;

        if (aetherBody.FixtureList.Count > 0)
        {
            aetherBody.FixtureList[0].CollisionCategories = PhysicsLayers.LocalPlayer;
            aetherBody.FixtureList[0].CollidesWith = PhysicsLayers.Environment | PhysicsLayers.EnemyAndProjectiles;
        }

        vehicle.Set(new PhysicsComponents.PhysicsBody { Value = aetherBody });
        NetworkRegistry.Add(netId, vehicle);
    }

    private static void BuildGenericInteractable(FlecsWorld ecsWorld, LdtkEntityInstance interactable, string activeDimension, ulong netId, ulong hostId)
    {
        float entWidth = interactable.Width > 0 ? interactable.Width : 16f;
        float entHeight = interactable.Height > 0 ? interactable.Height : 16f;
        float centerX = interactable.Px[0] + (entWidth / 2f);
        float centerY = interactable.Px[1] + (entHeight / 2f);

        int savedState = GetSavedState(interactable.Iid, ref centerX, ref centerY);

        var entity = ecsWorld.Entity()
            .Add<MatchEntityTag>()
            .Add<InteractableTag>()
            .Set(new Position { X = centerX, Y = centerY })
            .Set(new NetworkId { Value = netId })
            .Set(new NetworkOwner { Value = hostId })
            .Set(new PhysicsDimension { Name = activeDimension })
            .Set(new WorldMark { UniqueMarkId = interactable.Iid, InteractionState = savedState });

        if (savedState > 0) entity.Remove<InteractableTag>();

        ulong parentNetId = 0;
        int lastUnderscore = activeDimension.LastIndexOf('_');
        if (lastUnderscore >= 0) ulong.TryParse(activeDimension.Substring(lastUnderscore + 1), out parentNetId);

        if (interactable.Identifier == "AirlockDoor")
        {
            string targetDim = "MacroSpace";
            if (interactable.FieldInstances != null)
            {
                var destField = interactable.FieldInstances.FirstOrDefault(f => f.Identifier.Equals("TargetDimension", StringComparison.OrdinalIgnoreCase));
                if (destField != null && destField.Value != null) targetDim = destField.Value.ToString()!;
            }
            entity.Set(new PortalComponent { DestinationDimension = targetDim, IsVehicleExit = (parentNetId != 0), ParentVehicleNetId = parentNetId });
        }
        else if (interactable.Identifier == "PilotSeat") entity.Add<PilotSeatComponent>().Set(new PilotSeatComponent { VehicleNetId = parentNetId });
        else if (interactable.Identifier == "GunnerSeat") entity.Add<GunnerSeatComponent>().Set(new GunnerSeatComponent { VehicleNetId = parentNetId });

        NetworkRegistry.Add(netId, entity);
    }

    private static int GetSavedState(string uniqueId, ref float x, ref float y)
    {
        if (SaveManager.CurrentProfile != null && SaveManager.CurrentProfile.PersistentWorldMarks.TryGetValue(uniqueId, out var savedMark))
        {
            if (savedMark.X != 0 || savedMark.Y != 0) { x = savedMark.X; y = savedMark.Y; }
            return savedMark.State;
        }
        return 0;
    }
}
