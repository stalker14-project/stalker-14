using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Client._Stalker.Utilities.BoxExtensions;
using Content.Client.Hands.Systems;
using Content.Client.Items.Systems;
using Content.Client.Storage;
using Content.Client.Storage.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Input;
using Content.Shared.Item;
using Content.Shared.Storage;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Collections;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Storage.Controls;

public sealed class StorageWindow : BaseWindow
{
    [Dependency] private readonly IEntityManager _entity = default!;
    private readonly StorageUIController _storageController;

    public EntityUid? StorageEntity;

    private readonly GridContainer _pieceGrid;
    private readonly GridContainer _backgroundGrid;
    private readonly GridContainer _sidebar;

    private Control _titleContainer;
    private Label _titleLabel;

    // Needs to be nullable in case a piece is in default spot.
    private readonly Dictionary<EntityUid, (ItemStorageLocation? Loc, ItemGridPiece Control)> _pieces = new();
    private readonly List<Control> _controlGrid = new();

    private ValueList<EntityUid> _contained = new();
    private ValueList<EntityUid> _toRemove = new();

    // Manually store this because you can't have a 0x0 GridContainer but we still need to add child controls for 1x1 containers.
    private Vector2i _pieceGridSize;

    private TextureButton? _backButton;

    private bool _isDirty;

    public event Action<GUIBoundKeyEventArgs, ItemGridPiece>? OnPiecePressed;
    public event Action<GUIBoundKeyEventArgs, ItemGridPiece>? OnPieceUnpressed;

    private readonly string _emptyTexturePath = "Storage/tile_empty";
    private Texture? _emptyTexture;
    private readonly string _blockedTexturePath = "Storage/tile_blocked";
    private Texture? _blockedTexture;
    private readonly string _emptyOpaqueTexturePath = "Storage/tile_empty_opaque";
    private Texture? _emptyOpaqueTexture;
    private readonly string _blockedOpaqueTexturePath = "Storage/tile_blocked_opaque";
    private Texture? _blockedOpaqueTexture;
    private readonly string _exitTexturePath = "Storage/exit";
    private Texture? _exitTexture;
    private readonly string _backTexturePath = "Storage/back";
    private Texture? _backTexture;
    private readonly string _sidebarTopTexturePath = "Storage/sidebar_top";
    private Texture? _sidebarTopTexture;
    private readonly string _sidebarMiddleTexturePath = "Storage/sidebar_mid";
    private Texture? _sidebarMiddleTexture;
    private readonly string _sidebarBottomTexturePath = "Storage/sidebar_bottom";
    private Texture? _sidebarBottomTexture;
    private readonly string _sidebarFatTexturePath = "Storage/sidebar_fat";
    private Texture? _sidebarFatTexture;

    // Stalker-Changes-Start
    private readonly Dictionary<ConnectionState, Texture?> _textureMapping;
    private record ConnectionState(bool Top, bool Bottom, bool Left, bool Right);
    public event Action? OnCraftButtonPressed;
    public event Action? OnDisassembleButtonPressed;
    private readonly string _addTexturePath = "/Textures/_Stalker/Interface/STDefault/Storage/tile_empty_add";
    private Texture? _addEmptyTexture;
    private readonly string _bottomLeftTexturePath = "/Textures/_Stalker/Interface/STDefault/Storage/tile_empty_bottom_left";
    private Texture? _bottomLeftEmptyTexture;
    private readonly string _topLeftTexturePath = "/Textures/_Stalker/Interface/STDefault/Storage/tile_empty_top_left";
    private Texture? _topLeftEmptyTexture;
    private readonly string _bottomRightTexturePath = "/Textures/_Stalker/Interface/STDefault/Storage/tile_empty_bottom_right";
    private Texture? _bottomRightEmptyTexture;
    private readonly string _topRightTexturePath = "/Textures/_Stalker/Interface/STDefault/Storage/tile_empty_top_right";
    private Texture? _topRightEmptyTexture;
    private readonly string _leftBoundaryTexturePath = "/Textures/_Stalker/Interface/STDefault/Storage/tile_empty_boundary_left";
    private Texture? _leftBoundaryEmptyTexture;
    private readonly string _rightBoundaryTexturePath = "/Textures/_Stalker/Interface/STDefault/Storage/tile_empty_boundary_right";
    private Texture? _rightBoundaryEmptyTexture;
    private readonly string _bottomBoundaryTexturePath = "/Textures/_Stalker/Interface/STDefault/Storage/tile_empty_boundary_bottom";
    private Texture? _bottomBoundaryEmptyTexture;
    private readonly string _topBoundaryTexturePath = "/Textures/_Stalker/Interface/STDefault/Storage/tile_empty_boundary_top";
    private Texture? _topBoundaryEmptyTexture;
    private readonly string _craftTexturePath = "/Textures/_Stalker/Interface/STDefault/Storage/craft";
    private Texture? _craftTexture;
    private readonly string _disassebleTexturePath = "/Textures/_Stalker/Interface/STDefault/Storage/disasseble";
    private Texture? _disassembleTexture;
    // Stalker-Changes-End
    public StorageWindow()
    {
        IoCManager.InjectDependencies(this);
        Resizable = false;

        _storageController = UserInterfaceManager.GetUIController<StorageUIController>();

        OnThemeUpdated();

        MouseFilter = MouseFilterMode.Stop;

        _sidebar = new GridContainer
        {
            Name = "SideBar",
            HSeparationOverride = 0,
            VSeparationOverride = 0,
            Columns = 1
        };

        _pieceGrid = new GridContainer
        {
            Name = "PieceGrid",
            HSeparationOverride = 0,
            VSeparationOverride = 0
        };

        _backgroundGrid = new GridContainer
        {
            Name = "BackgroundGrid",
            HSeparationOverride = 0,
            VSeparationOverride = 0
        };

        _titleLabel = new Label()
        {
            HorizontalExpand = true,
            Name = "StorageLabel",
            ClipText = true,
            Text = Loc.GetString("comp-storage-window-dummy"),
            StyleClasses =
            {
                "FancyWindowTitle",
            }
        };

        _titleContainer = new PanelContainer()
        {
            StyleClasses =
            {
                "WindowHeadingBackground"
            },
            Children =
            {
                _titleLabel
            }
        };

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children =
            {
                _titleContainer,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children =
                    {
                        _sidebar,
                        new Control
                        {
                            Children =
                            {
                                _backgroundGrid,
                                _pieceGrid
                            }
                        }
                    }
                }
            }
        };

        AddChild(container);

        _textureMapping = new Dictionary<ConnectionState, Texture?>
        {
            { new ConnectionState(true, true, true, true), _emptyTexture },
            { new ConnectionState(true, false, false, true), _bottomRightEmptyTexture },
            { new ConnectionState(true, false, true, false), _bottomLeftEmptyTexture },
            { new ConnectionState(false, true, false, true), _topRightEmptyTexture },
            { new ConnectionState(false, true, true, false), _topLeftEmptyTexture },
            { new ConnectionState(false, true, true, true), _topBoundaryEmptyTexture },
            { new ConnectionState(true, true, false, true), _rightBoundaryEmptyTexture },
            { new ConnectionState(true, true, true, false), _leftBoundaryEmptyTexture },
            { new ConnectionState(true, false, true, true), _bottomBoundaryEmptyTexture }
        };
    }

    protected override void OnThemeUpdated()
    {
        base.OnThemeUpdated();
        // Stalker-Changes-Start
        _addEmptyTexture = Theme.ResolveTextureOrNull(_addTexturePath)?.Texture;
        _bottomLeftEmptyTexture = Theme.ResolveTextureOrNull(_bottomLeftTexturePath)?.Texture;
        _topLeftEmptyTexture = Theme.ResolveTextureOrNull(_topLeftTexturePath)?.Texture;
        _bottomRightEmptyTexture = Theme.ResolveTextureOrNull(_bottomRightTexturePath)?.Texture;
        _topRightEmptyTexture = Theme.ResolveTextureOrNull(_topRightTexturePath)?.Texture;
        _leftBoundaryEmptyTexture = Theme.ResolveTextureOrNull(_leftBoundaryTexturePath)?.Texture;
        _rightBoundaryEmptyTexture = Theme.ResolveTextureOrNull(_rightBoundaryTexturePath)?.Texture;
        _bottomBoundaryEmptyTexture = Theme.ResolveTextureOrNull(_bottomBoundaryTexturePath)?.Texture;
        _topBoundaryEmptyTexture = Theme.ResolveTextureOrNull(_topBoundaryTexturePath)?.Texture;
        _craftTexture = Theme.ResolveTextureOrNull(_craftTexturePath)?.Texture;
        _disassembleTexture = Theme.ResolveTextureOrNull(_disassebleTexturePath)?.Texture;
        // Stalker-Changes-End
        _emptyTexture = Theme.ResolveTextureOrNull(_emptyTexturePath)?.Texture;
        _blockedTexture = Theme.ResolveTextureOrNull(_blockedTexturePath)?.Texture;
        _emptyOpaqueTexture = Theme.ResolveTextureOrNull(_emptyOpaqueTexturePath)?.Texture;
        _blockedOpaqueTexture = Theme.ResolveTextureOrNull(_blockedOpaqueTexturePath)?.Texture;
        _exitTexture = Theme.ResolveTextureOrNull(_exitTexturePath)?.Texture;
        _backTexture = Theme.ResolveTextureOrNull(_backTexturePath)?.Texture;
        _sidebarTopTexture = Theme.ResolveTextureOrNull(_sidebarTopTexturePath)?.Texture;
        _sidebarMiddleTexture = Theme.ResolveTextureOrNull(_sidebarMiddleTexturePath)?.Texture;
        _sidebarBottomTexture = Theme.ResolveTextureOrNull(_sidebarBottomTexturePath)?.Texture;
        _sidebarFatTexture = Theme.ResolveTextureOrNull(_sidebarFatTexturePath)?.Texture;
    }

    public void UpdateContainer(Entity<StorageComponent>? entity)
    {
        Visible = entity != null;
        StorageEntity = entity;
        if (entity == null)
            return;

        if (UserInterfaceManager.GetUIController<StorageUIController>().WindowTitle)
        {
            _titleLabel.Text = Identity.Name(entity.Value, _entity);
            _titleContainer.Visible = true;
        }
        else
        {
            _titleContainer.Visible = false;
        }

        BuildGridRepresentation();
    }

    private void CloseParent()
    {
        if (StorageEntity == null)
            return;

        var containerSystem = _entity.System<SharedContainerSystem>();
        var uiSystem = _entity.System<UserInterfaceSystem>();

        if (containerSystem.TryGetContainingContainer(StorageEntity.Value, out var container) &&
            _entity.TryGetComponent(container.Owner, out StorageComponent? storage) &&
            storage.Container.Contains(StorageEntity.Value) &&
            uiSystem
                .TryGetOpenUi<StorageBoundUserInterface>(container.Owner,
                    StorageComponent.StorageUiKey.Key,
                    out var parentBui))
        {
            parentBui.CloseWindow(Position);
        }
    }

    private void BuildGridRepresentation()
    {
        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var comp) || comp.Grid.Count == 0)
            return;

        var boundingGrid = comp.Grid.GetBoundingBox();

        BuildBackground();

        #region Sidebar
        _sidebar.Children.Clear();
        _sidebar.Rows = boundingGrid.Height + 1;
        var craftButton = new TextureButton // Stalker-Changes-Start
        {
            TextureNormal = _craftTexture,
            Scale = new Vector2(2, 2),
            Visible = comp.Craft,
        };
        craftButton.OnPressed += _ => OnCraftButtonPressed?.Invoke();
        var diassembleButton = new TextureButton
        {
            TextureNormal = _disassembleTexture,
            Scale = new Vector2(2, 2),
            Visible = comp.Disassemble
        };
        diassembleButton.OnPressed += _ => OnDisassembleButtonPressed?.Invoke();

        var craftContainer = new BoxContainer
        {
            Children =
            {
                new TextureRect
                {
                    Texture = boundingGrid.Height == 1 ? _sidebarBottomTexture : _sidebarMiddleTexture,
                    TextureScale = new Vector2(2, 2),
                    Children =
                    {
                        craftButton
                    }
                }
            }
        };
        var disassembleContainer = new BoxContainer
        {
            Children =
            {
                new TextureRect
                {
                    Texture = boundingGrid.Height == 1 ? _sidebarBottomTexture : _sidebarMiddleTexture,
                    TextureScale = new Vector2(2, 2),
                    Children =
                    {
                        diassembleButton
                    }
                }
            }
        };

        // Stalker-Changes-End

        var rows = boundingGrid.Height + 1;
        _sidebar.Rows = rows;

        var exitButton = new TextureButton
        {
            Name = "ExitButton",
            TextureNormal = _exitTexture,
            Scale = new Vector2(2, 2),
        };
        exitButton.OnPressed += _ =>
        {
            // Close ourselves and all parent BUIs.
            Close();
            CloseParent();
        };
        exitButton.OnKeyBindDown += args =>
        {
            // it just makes sense...
            if (!args.Handled && args.Function == ContentKeyFunctions.ActivateItemInWorld)
            {
                Close();
                CloseParent();
                args.Handle();
            }
        };

        var exitContainer = new BoxContainer
        {
            Name = "ExitContainer",
            Children =
            {
                new TextureRect
                {
                    Texture = boundingGrid.Height != 0
                        ? _sidebarTopTexture
                        : _sidebarFatTexture,
                    TextureScale = new Vector2(2, 2),
                    Children =
                    {
                        exitButton
                    }
                }
            }
        };

        _sidebar.AddChild(exitContainer);
        _sidebar.AddChild(craftContainer); // Stalker-Changes
        _sidebar.AddChild(disassembleContainer); // Stalker-Changes
        for (var i = 0; i < boundingGrid.Height - 3; i++) // Stalker-Changes
        {
            _sidebar.AddChild(new TextureRect
            {
                Texture = _sidebarMiddleTexture,
                TextureScale = new Vector2(2, 2),
            });
            var offset = 2;

            if (_entity.System<StorageSystem>().NestedStorage && rows > 0)
            {
                _backButton = new TextureButton
                {
                    TextureNormal = _backTexture,
                    Scale = new Vector2(2, 2),
                };
                _backButton.OnPressed += _ =>
                {
                    var containerSystem = _entity.System<SharedContainerSystem>();

                    if (containerSystem.TryGetContainingContainer(StorageEntity.Value, out var container) &&
                        _entity.TryGetComponent(container.Owner, out StorageComponent? storage) &&
                        storage.Container.Contains(StorageEntity.Value))
                    {
                        Close();

                        if (_entity.System<SharedUserInterfaceSystem>()
                            .TryGetOpenUi<StorageBoundUserInterface>(container.Owner,
                                StorageComponent.StorageUiKey.Key,
                                out var parentBui))
                        {
                            parentBui.Show(Position);
                        }
                    }
                };

                var backContainer = new BoxContainer
                {
                    Name = "ExitContainer",
                    Children =
                {
                    new TextureRect
                    {
                        Texture = rows > 2 ? _sidebarMiddleTexture : _sidebarBottomTexture,
                        TextureScale = new Vector2(2, 2),
                        Children =
                        {
                            _backButton,
                        }
                    }
                }
                };

                _sidebar.AddChild(backContainer);
            }

            if (boundingGrid.Height != 1) // Stalker-Changes-Start
            {
                if (boundingGrid.Height > 0)
                {
                    _sidebar.AddChild(new TextureRect
                    {
                        Texture = _sidebarBottomTexture,
                        TextureScale = new Vector2(2, 2),
                    });
                }
            } // Stalker-Changes-End

            #endregion

            FlagDirty();
        }
    }

    public void BuildBackground()
    {
        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var comp) || !comp.Grid.Any())
            return;

        var boundingGrid = comp.Grid.GetBoundingBox();

        var emptyTexture = _storageController.OpaqueStorageWindow
            ? _emptyOpaqueTexture
            : _emptyTexture;
        var blockedTexture = _storageController.OpaqueStorageWindow
            ? _blockedOpaqueTexture
            : _blockedTexture;

        _backgroundGrid.Children.Clear();
        _backgroundGrid.Rows = boundingGrid.Height + 1;
        _backgroundGrid.Columns = boundingGrid.Width + 1;
        for (var y = boundingGrid.Bottom; y <= boundingGrid.Top; y++)
        {
            for (var x = boundingGrid.Left; x <= boundingGrid.Right; x++)
            {
                var texture = comp.Grid.Contains(x, y)
                    ? GetAppropriateTexture(comp.Grid, new Vector2i(x, y)) // Stalker-Changes
                    : blockedTexture;

                _backgroundGrid.AddChild(new TextureRect
                {
                    Texture = texture,
                    TextureScale = new Vector2(2, 2)
                });
            }
        }
    }

    public void Reclaim(ItemStorageLocation location, ItemGridPiece draggingGhost)
    {
        draggingGhost.OnPiecePressed += OnPiecePressed;
        draggingGhost.OnPieceUnpressed += OnPieceUnpressed;
        _pieces[draggingGhost.Entity] = (location, draggingGhost);
        draggingGhost.Location = location;
        var controlIndex = GetGridIndex(draggingGhost);
        _controlGrid[controlIndex].AddChild(draggingGhost);
    }

    private int GetGridIndex(ItemGridPiece piece)
    {
        return piece.Location.Position.X + piece.Location.Position.Y * _pieceGrid.Columns;
    }

    public void FlagDirty()
    {
        _isDirty = true;
    }

    public void RemoveGrid(ItemGridPiece control)
    {
        control.Orphan();
        _pieces.Remove(control.Entity);
        control.OnPiecePressed -= OnPiecePressed;
        control.OnPieceUnpressed -= OnPieceUnpressed;
    }

    public void BuildItemPieces()
    {
        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var storageComp))
            return;

        if (storageComp.Grid.Count == 0)
            return;

        var boundingGrid = storageComp.Grid.GetBoundingBox();
        var size = _emptyTexture!.Size * 2;
        _contained.Clear();
        _contained.AddRange(storageComp.Container.ContainedEntities.Reverse());

        var width = boundingGrid.Width + 1;
        var height = boundingGrid.Height + 1;

        // Build the grid representation
         if (_pieceGrid.Rows != _pieceGridSize.Y || _pieceGrid.Columns != _pieceGridSize.X)
        {
            _pieceGrid.Rows = height;
            _pieceGrid.Columns = width;
            _controlGrid.Clear();

            for (var y = boundingGrid.Bottom; y <= boundingGrid.Top; y++)
            {
                for (var x = boundingGrid.Left; x <= boundingGrid.Right; x++)
                {
                    var control = new Control
                    {
                        MinSize = size
                    };

                    _controlGrid.Add(control);
                    _pieceGrid.AddChild(control);
                }
            }
        }

        _pieceGridSize = new(width, height);
        _toRemove.Clear();

        // Remove entities no longer relevant / Update existing ones
        foreach (var (ent, data) in _pieces)
        {
            if (storageComp.StoredItems.TryGetValue(ent, out var updated))
            {
                data.Control.Marked = IsMarked(ent);

                if (data.Loc.Equals(updated))
                {
                    DebugTools.Assert(data.Control.Location == updated);
                    continue;
                }

                // Update
                data.Control.Location = updated;
                var index = GetGridIndex(data.Control);
                data.Control.Orphan();
                _controlGrid[index].AddChild(data.Control);
                _pieces[ent] = (updated, data.Control);
                continue;
            }

            _toRemove.Add(ent);
        }

        foreach (var ent in _toRemove)
        {
            _pieces.Remove(ent, out var data);
            data.Control.Orphan();
        }

        // Add new ones
        foreach (var (ent, loc) in storageComp.StoredItems)
        {
            if (_pieces.TryGetValue(ent, out var existing))
            {
                DebugTools.Assert(existing.Loc == loc);
                continue;
            }

            if (_entity.TryGetComponent<ItemComponent>(ent, out var itemEntComponent))
            {
                var gridPiece = new ItemGridPiece((ent, itemEntComponent), loc, _entity)
                {
                    MinSize = size,
                    Marked = IsMarked(ent),
                };
                gridPiece.OnPiecePressed += OnPiecePressed;
                gridPiece.OnPieceUnpressed += OnPieceUnpressed;
                var controlIndex = loc.Position.X + loc.Position.Y * (boundingGrid.Width + 1);

                _controlGrid[controlIndex].AddChild(gridPiece);
                _pieces[ent] = (loc, gridPiece);
            }
        }
    }

    private ItemGridPieceMarks? IsMarked(EntityUid uid)
    {
        return _contained.IndexOf(uid) switch
        {
            0 => ItemGridPieceMarks.First,
            1 => ItemGridPieceMarks.Second,
            _ => null,
        };
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!IsOpen)
            return;

        if (_isDirty)
        {
            _isDirty = false;
            BuildItemPieces();
        }

        var containerSystem = _entity.System<SharedContainerSystem>();

        if (_backButton != null)
        {
            if (StorageEntity != null && _entity.System<StorageSystem>().NestedStorage)
            {
                // If parent container nests us then show back button
                if (containerSystem.TryGetContainingContainer(StorageEntity.Value, out var container) &&
                    _entity.TryGetComponent(container.Owner, out StorageComponent? storageComp) && storageComp.Container.Contains(StorageEntity.Value))
                {
                    _backButton.Visible = true;
                }
                else
                {
                    _backButton.Visible = false;
                }
            }
            // Hide the button.
            else
            {
                _backButton.Visible = false;
            }
        }

        var itemSystem = _entity.System<ItemSystem>();
        var storageSystem = _entity.System<StorageSystem>();
        var handsSystem = _entity.System<HandsSystem>();

        foreach (var child in _backgroundGrid.Children)
        {
            child.ModulateSelfOverride = Color.FromHex("#FFFFFF"); // Stalker-Changes | White because default #222222 makes it black, IDK why.
        }

        if (UserInterfaceManager.CurrentlyHovered is StorageWindow con && con != this)
            return;

        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var storageComponent))
            return;

        EntityUid currentEnt;
        ItemStorageLocation currentLocation;
        var usingInHand = false;
        if (_storageController.IsDragging && _storageController.DraggingGhost is { } dragging)
        {
            currentEnt = dragging.Entity;
            currentLocation = dragging.Location;
        }
        else if (handsSystem.GetActiveHandEntity() is { } handEntity &&
                 storageSystem.CanInsert(StorageEntity.Value, handEntity, out _, storageComp: storageComponent, ignoreLocation: true))
        {
            currentEnt = handEntity;
            currentLocation = new ItemStorageLocation(_storageController.DraggingRotation, Vector2i.Zero);
            usingInHand = true;
        }
        else
        {
            return;
        }

        if (!_entity.TryGetComponent<ItemComponent>(currentEnt, out var itemComp))
            return;

        var origin = GetMouseGridPieceLocation((currentEnt, itemComp), currentLocation);

        var itemShape = itemSystem.GetAdjustedItemShape(
            (currentEnt, itemComp),
            currentLocation.Rotation,
            origin);
        var itemBounding = itemShape.GetBoundingBox();

        var validLocation = storageSystem.ItemFitsInGridLocation(
            (currentEnt, itemComp),
            (StorageEntity.Value, storageComponent),
            origin,
            currentLocation.Rotation);

        foreach (var locations in storageComponent.SavedLocations)
        {
            if (!_entity.TryGetComponent<MetaDataComponent>(currentEnt, out var meta) || meta.EntityName != locations.Key)
                continue;

            float spot = 0;
            var marked = new ValueList<Control>();

            foreach (var location in locations.Value)
            {
                var shape = itemSystem.GetAdjustedItemShape(currentEnt, location);
                var bound = shape.GetBoundingBox();

                var spotFree = storageSystem.ItemFitsInGridLocation(currentEnt, StorageEntity.Value, location);

                if (spotFree)
                    spot++;

                for (var y = bound.Bottom; y <= bound.Top; y++)
                {
                    for (var x = bound.Left; x <= bound.Right; x++)
                    {
                        if (TryGetBackgroundCell(x, y, out var cell) && shape.Contains(x, y) && !marked.Contains(cell))
                        {
                            marked.Add(cell);
                            cell.ModulateSelfOverride = spotFree
                                ? Color.FromHsv(new Vector4(0.18f, 1 / spot, 0.5f / spot + 0.5f, 1f))
                                : Color.FromHex("#2222CC");
                        }
                    }
                }
            }
        }

        var validColor = usingInHand ? Color.Goldenrod : Color.FromHex("#1E8000");

        for (var y = itemBounding.Bottom; y <= itemBounding.Top; y++)
        {
            for (var x = itemBounding.Left; x <= itemBounding.Right; x++)
            {
                if (TryGetBackgroundCell(x, y, out var cell) && itemShape.Contains(x, y))
                {
                    cell.ModulateSelfOverride = validLocation ? validColor : Color.FromHex("#B40046");
                }
            }
        }
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        if (_storageController.StaticStorageUIEnabled)
            return DragMode.None;

        if (_sidebar.SizeBox.Contains(relativeMousePos - _sidebar.Position))
        {
            return DragMode.Move;
        }

        return DragMode.None;
    }

    public Vector2i GetMouseGridPieceLocation(Entity<ItemComponent?> entity, ItemStorageLocation location)
    {
        var origin = Vector2i.Zero;

        if (StorageEntity != null)
            origin = _entity.GetComponent<StorageComponent>(StorageEntity.Value).Grid.GetBoundingBox().BottomLeft;

        var textureSize = (Vector2) _emptyTexture!.Size * 2;
        var position = ((UserInterfaceManager.MousePositionScaled.Position
                         - _backgroundGrid.GlobalPosition
                         - ItemGridPiece.GetCenterOffset(entity, location, _entity) * 2
                         + textureSize / 2f)
                        / textureSize).Floored() + origin;
        return position;
    }

    public bool TryGetBackgroundCell(int x, int y, [NotNullWhen(true)] out Control? cell)
    {
        cell = null;

        if (!_entity.TryGetComponent<StorageComponent>(StorageEntity, out var storageComponent))
            return false;
        var boundingBox = storageComponent.Grid.GetBoundingBox();
        x -= boundingBox.Left;
        y -= boundingBox.Bottom;

        if (x < 0 ||
            x >= _backgroundGrid.Columns ||
            y < 0 ||
            y >= _backgroundGrid.Rows)
        {
            return false;
        }

        cell = _backgroundGrid.GetChild(y * _backgroundGrid.Columns + x);
        return true;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (!IsOpen)
            return;

        var storageSystem = _entity.System<StorageSystem>();
        var handsSystem = _entity.System<HandsSystem>();

        if (args.Function == ContentKeyFunctions.MoveStoredItem && StorageEntity != null)
        {
            if (handsSystem.GetActiveHandEntity() is { } handEntity &&
                storageSystem.CanInsert(StorageEntity.Value, handEntity, out _))
            {
                var pos = GetMouseGridPieceLocation((handEntity, null),
                    new ItemStorageLocation(_storageController.DraggingRotation, Vector2i.Zero));

                var insertLocation = new ItemStorageLocation(_storageController.DraggingRotation, pos);
                if (storageSystem.ItemFitsInGridLocation(
                        (handEntity, null),
                        (StorageEntity.Value, null),
                        insertLocation))
                {
                    _entity.RaisePredictiveEvent(new StorageInsertItemIntoLocationEvent(
                        _entity.GetNetEntity(handEntity),
                        _entity.GetNetEntity(StorageEntity.Value),
                        insertLocation));
                    _storageController.DraggingRotation = Angle.Zero;
                    args.Handle();
                }
            }
        }
    }


// Stalker-Changes-starts

    private Texture? GetAppropriateTexture(List<Box2i> grid, Vector2i position)
    {
        var hashSet = new HashSet<Vector2i>(grid.SelectMany(BoxExtensions.GetAllPoints));

        var top = hashSet.Contains(position - Vector2i.Up);
        var bottom = hashSet.Contains(position - Vector2i.Down);
        var left = hashSet.Contains(position - Vector2i.Left);
        var right = hashSet.Contains(position - Vector2i.Right);

        ConnectionState key = new(top, bottom, left, right);

        return _textureMapping.GetValueOrDefault(key, _addEmptyTexture);
    }
// Stalker-Changes-Ends
}
