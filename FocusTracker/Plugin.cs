using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;

using System.Numerics;

namespace FocusTracker;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    internal static ITargetManager TargetManager { get; private set; } = null!;

    [PluginService]
    internal static IGameGui GameGui { get; private set; } = null!;

    [PluginService]
    internal static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    internal static IObjectTable ObjectTable { get; private set; } = null!;

    [PluginService]
    internal static IChatGui ChatGui { get; private set; } = null!;

    [PluginService]
    internal static IPluginLog Log { get; private set; } = null!;


    private const string CommandName = "/ftrack";

    private bool markerEnabled = true;

    // =========================
    // 我们自己记住的目标
    // =========================

    private ulong trackedGameObjectId = 0;
    private uint trackedEntityId = 0;

    private string trackedName = "";

    private Vector3 lastKnownPosition;

    private bool hasTrackedTarget = false;


    public Plugin()
    {
        CommandManager.AddHandler(
            CommandName,
            new CommandInfo(OnCommand)
            {
                HelpMessage = "锁定当前焦点目标，或开关追踪标记"
            }
        );

        PluginInterface.UiBuilder.Draw += Draw;

        // 换 Territory 时清空
        ClientState.TerritoryChanged += OnTerritoryChanged;

        Log.Information("FocusTracker loaded.");
    }


    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= Draw;

        ClientState.TerritoryChanged -= OnTerritoryChanged;

        CommandManager.RemoveHandler(CommandName);
    }


    // =========================
    // /ftrack
    // =========================

    private void OnCommand(string command, string args)
    {
        var focus = TargetManager.FocusTarget;

        // 有焦点目标：
        // 将当前焦点设为我们的长期追踪目标
        if (focus != null)
        {
            trackedGameObjectId = focus.GameObjectId;
            trackedEntityId = focus.EntityId;

            trackedName = focus.Name.ToString();

            lastKnownPosition = focus.Position;

            hasTrackedTarget = true;
            markerEnabled = true;

            ChatGui.Print(
                $"[FocusTracker] 开始追踪：{trackedName}"
            );

            return;
        }

        // 没有焦点目标时，/ftrack 用作开关
        markerEnabled = !markerEnabled;

        ChatGui.Print(
            markerEnabled
                ? "[FocusTracker] 追踪标记已开启。"
                : "[FocusTracker] 追踪标记已关闭。"
        );
    }


    // =========================
    // 换地图 / Territory
    // =========================

    private void OnTerritoryChanged(uint territoryId)
    {
        ClearTrackedTarget();

   
       
    }


    private void ClearTrackedTarget()
    {
        trackedGameObjectId = 0;
        trackedEntityId = 0;

        trackedName = "";

        lastKnownPosition = Vector3.Zero;

        hasTrackedTarget = false;
    }


    // =========================
    // 尝试取得当前追踪对象
    // =========================

    private Dalamud.Game.ClientState.Objects.Types.IGameObject?
        GetTrackedObject()
    {
        if (!hasTrackedTarget)
            return null;


        // 第一优先：GameObjectId
        if (trackedGameObjectId != 0)
        {
            var obj =
                ObjectTable.SearchById(
                    trackedGameObjectId
                );

            if (obj != null)
                return obj;
        }


        // 第二优先：EntityId
        if (trackedEntityId != 0)
        {
            var obj =
                ObjectTable.SearchByEntityId(
                    trackedEntityId
                );

            if (obj != null)
                return obj;
        }


        return null;
    }


    // =========================
    // Draw
    // =========================

    private void Draw()
    {
        if (!markerEnabled)
            return;

        if (!ClientState.IsLoggedIn)
            return;


        // =========================
        // 如果当前又有 Focus，
        // 自动更新追踪目标
        // =========================

        var focus = TargetManager.FocusTarget;

        if (focus != null)
        {
            // 如果是新的焦点
            if (!hasTrackedTarget ||
                focus.GameObjectId != trackedGameObjectId)
            {
                trackedGameObjectId =
                    focus.GameObjectId;

                trackedEntityId =
                    focus.EntityId;

                trackedName =
                    focus.Name.ToString();

                hasTrackedTarget = true;
            }

            lastKnownPosition =
                focus.Position;
        }


        if (!hasTrackedTarget)
            return;


        // =========================
        // 即使 Focus 已经消失，
        // 仍然从 ObjectTable 找
        // =========================

        var trackedObject =
            GetTrackedObject();


        Vector3 targetPosition;


        if (trackedObject != null)
        {
            // 目标仍然存在
            targetPosition =
                trackedObject.Position;

            lastKnownPosition =
                targetPosition;

            trackedName =
                trackedObject.Name.ToString();
        }
        else
        {
            // 目标已经从 ObjectTable 消失
            // 先使用最后一次坐标
            targetPosition =
                lastKnownPosition;
        }


        // =========================
        // 头顶位置
        // =========================

        var markerWorldPosition =
            targetPosition;

        markerWorldPosition.Y += 2.8f;


        if (!GameGui.WorldToScreen(
                markerWorldPosition,
                out Vector2 screenPosition))
        {
            return;
        }


        var drawList =
            ImGui.GetForegroundDrawList();


        uint red =
            ImGui.GetColorU32(
                new Vector4(
                    1f,
                    0.1f,
                    0.1f,
                    1f
                )
            );

        uint white =
            ImGui.GetColorU32(
                new Vector4(
                    1f,
                    1f,
                    1f,
                    1f
                )
            );

        uint black =
            ImGui.GetColorU32(
                new Vector4(
                    0f,
                    0f,
                    0f,
                    1f
                )
            );


        // =========================
        // 三角
        // =========================

        float triangleWidth = 24f;
        float triangleHeight = 28f;


        Vector2 topLeft = new(
            screenPosition.X - triangleWidth,
            screenPosition.Y - triangleHeight
        );


        Vector2 topRight = new(
            screenPosition.X + triangleWidth,
            screenPosition.Y - triangleHeight
        );


        Vector2 bottom = new(
            screenPosition.X,
            screenPosition.Y
        );


        drawList.AddTriangleFilled(
            topLeft,
            topRight,
            bottom,
            red
        );


        // =========================
        // 名字
        // =========================

        Vector2 nameSize =
            ImGui.CalcTextSize(
                trackedName
            );


        Vector2 namePosition = new(
            screenPosition.X
                - nameSize.X / 2f,

            screenPosition.Y
                - 58f
        );


        DrawOutlinedText(
            drawList,
            namePosition,
            trackedName,
            white,
            black
        );


        // =========================
        // 距离
        // =========================

        var localPlayer =
            ObjectTable.LocalPlayer;


        if (localPlayer == null)
            return;


        float distance =
            Vector3.Distance(
                localPlayer.Position,
                targetPosition
            );


        string distanceText =
            $"{distance:F1}y";


        // 如果对象已经消失，
        // 明确告诉你这是最后位置
        if (trackedObject == null)
        {
            distanceText += " LAST";
        }


        Vector2 distanceSize =
            ImGui.CalcTextSize(
                distanceText
            );


        Vector2 distancePosition = new(
            screenPosition.X
                - distanceSize.X / 2f,

            screenPosition.Y
                - 40f
        );


        DrawOutlinedText(
            drawList,
            distancePosition,
            distanceText,
            white,
            black
        );
    }


    private static void DrawOutlinedText(
        ImDrawListPtr drawList,
        Vector2 position,
        string text,
        uint textColor,
        uint outlineColor)
    {
        drawList.AddText(
            position + new Vector2(-1f, 0f),
            outlineColor,
            text
        );

        drawList.AddText(
            position + new Vector2(1f, 0f),
            outlineColor,
            text
        );

        drawList.AddText(
            position + new Vector2(0f, -1f),
            outlineColor,
            text
        );

        drawList.AddText(
            position + new Vector2(0f, 1f),
            outlineColor,
            text
        );

        drawList.AddText(
            position,
            textColor,
            text
        );
    }
}
