using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.HoYoPlay;
using Starward.Frameworks;
using Starward.Helpers;
using Starward.Messages;
using Starward.Services.Launcher;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Text;
using Windows.Foundation;


namespace Starward.Features.GameSelector;

[INotifyPropertyChanged]
public sealed partial class GameSelector : UserControl
{

    private const string IconPin = "\uE718";

    private const string IconUnpin = "\uE77A";


    public event EventHandler<(GameId, bool DoubleTapped)>? CurrentGameChanged;


    private HoYoPlayService _hoyoplayService = AppService.GetService<HoYoPlayService>();



    public GameSelector()
    {
        this.InitializeComponent();
        InitializeGameSelector();
    }



    public GameBiz CurrentGameBiz { get; set; }


    public GameId? CurrentGameId { get; set; }


    public ObservableCollection<GameBizIcon> GameBizIcons { get; set => SetProperty(ref field, value); } = new();


    public GameBizIcon? CurrentGameBizIcon { get; set => SetProperty(ref field, value); }


    public bool IsPinned { get; set => SetProperty(ref field, value); }




    public void InitializeGameSelector()
    {
        GameBizIcons.Clear();

        // �������ļ���ȡ��ѡ�� GameBiz
        string? bizs = AppSetting.SelectedGameBizs;
        foreach (string str in bizs?.Split(',') ?? [])
        {
            if (GameBiz.TryParse(str, out GameBiz biz))
            {
                // ��֪�� GameBiz
                GameBizIcons.Add(new GameBizIcon(biz));
            }
            else if (_hoyoplayService.GetCachedGameInfo(biz) is GameInfo info)
            {
                // �� HoYoPlay API ��ȡ����δ����� GameBiz
                GameBizIcons.Add(new GameBizIcon(info));
            }
        }

        // ѡȡ��ǰ��Ϸ
        GameBiz lastSelectedGameBiz = AppSetting.CurrentGameBiz;
        if (GameBizIcons.FirstOrDefault(x => x.GameBiz == lastSelectedGameBiz) is GameBizIcon icon)
        {
            CurrentGameBizIcon = icon;
            CurrentGameBizIcon.IsSelected = true;
            CurrentGameBiz = lastSelectedGameBiz;
        }
        else if (lastSelectedGameBiz.IsKnown())
        {
            CurrentGameBizIcon = new GameBizIcon(lastSelectedGameBiz);
            CurrentGameBiz = lastSelectedGameBiz;
        }

        CurrentGameId = CurrentGameBizIcon?.GameId;
        if (CurrentGameId is not null)
        {
            CurrentGameChanged?.Invoke(this, (CurrentGameId, false));
        }

        if (AppSetting.IsGameBizSelectorPinned)
        {
            Pin();
        }
    }



    [RelayCommand]
    public void AutoSearchInstalledGames()
    {
        try
        {
            // todo ��ע����Զ������Ѱ�װ����Ϸ
            var service = AppService.GetService<GameLauncherService>();
            var sb = new StringBuilder();
            foreach (GameBiz biz in GameBiz.AllGameBizs)
            {
                if (service.IsGameExeExists(biz))
                {
                    sb.Append(biz.ToString());
                    sb.Append(',');
                }
            }
            AppSetting.SelectedGameBizs = sb.ToString().TrimEnd(',');
            InitializeGameSelector();
        }
        catch (Exception ex)
        {

        }
    }




    #region Game Icon



    /// <summary>
    /// ��Ϸͼ�������Ƿ�ɼ�
    /// </summary>
    public bool GameIconsAreaVisible
    {
        get => Border_GameIconsArea.Translation == Vector3.Zero;
        set
        {
            if (value)
            {
                Border_GameIconsArea.Translation = Vector3.Zero;
            }
            else
            {
                Border_GameIconsArea.Translation = new Vector3(0, -100, 0);
            }
            UpdateDragRectangles();
        }
    }


    /// <summary>
    /// ���´�����ק����
    /// </summary>
    public void UpdateDragRectangles()
    {
        try
        {
            double x = Border_CurrentGameIcon.ActualWidth;
            if (GameIconsAreaVisible)
            {
                x = Border_CurrentGameIcon.ActualWidth + Border_GameIconsArea.ActualWidth;
            }
            this.XamlRoot.SetWindowDragRectangles([new Rect(x, 0, 10000, 48)]);
        }
        catch { }
    }



    /// <summary>
    /// ������뵽��ǰ��Ϸͼ��
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Border_CurrentGameIcon_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // ��ʾ������Ϸͼ��
        GameIconsAreaVisible = true;
    }



    /// <summary>
    /// ����Ƴ���ǰ��Ϸͼ��
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Border_CurrentGameIcon_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (FullBackgroundVisible || IsPinned)
        {
            // ��ǰ��Ϸͼ�걻�̶�����ȫ����ʾʱ��������������Ϸͼ��
            return;
        }
        if (sender is UIElement ele)
        {
            var postion = e.GetCurrentPoint(sender as UIElement).Position;
            if (postion.X > ele.ActualSize.X - 1 && postion.Y > 0 && postion.Y < ele.ActualSize.Y)
            {
                // ���Ҳ��Ƴ�����ʱ���뵽������Ϸͼ�����򣬲�����
                return;
            }
        }
        // ���������Ƴ�������������Ϸͼ��
        GameIconsAreaVisible = false;
    }



    /// <summary>
    /// ����Ƴ�������Ϸͼ������
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Border_GameIconsArea_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (FullBackgroundVisible || IsPinned)
        {
            return;
        }
        GameIconsAreaVisible = false;
    }



    /// <summary>
    /// ���û�б�ѡ�����Ϸͼ��
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Button_GameIcon_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is GameBizIcon icon)
        {
            if (CurrentGameBizIcon is not null)
            {
                CurrentGameBizIcon.IsSelected = false;
            }

            CurrentGameBizIcon = icon;
            CurrentGameBiz = icon.GameBiz;
            CurrentGameId = icon.GameId;
            icon.IsSelected = true;

            CurrentGameChanged?.Invoke(this, (icon.GameId, false));
            AppSetting.CurrentGameBiz = icon.GameBiz;
        }
    }



    /// <summary>
    /// ˫��û�б�ѡ�����Ϸͼ��
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Button_GameIcon_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is GameBizIcon icon)
        {
            if (CurrentGameBizIcon is not null)
            {
                CurrentGameBizIcon.IsSelected = false;
            }

            CurrentGameBizIcon = icon;
            CurrentGameBiz = icon.GameBiz;
            CurrentGameId = icon.GameId;
            icon.IsSelected = true;
            HideFullBackground();

            CurrentGameChanged?.Invoke(this, (icon.GameId, true));
            AppSetting.CurrentGameBiz = icon.GameBiz;
        }
    }


    /// <summary>
    /// ������뵽��Ϸͼ��
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Button_GameIcon_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is GameBizIcon icon)
        {
            if (!icon.IsSelected)
            {
                icon.MaskOpacity = 0;
            }
        }
    }



    /// <summary>
    /// ����Ƴ���Ϸͼ��
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Button_GameIcon_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is GameBizIcon icon)
        {
            if (!icon.IsSelected)
            {
                icon.MaskOpacity = 1;
            }
        }
    }



    /// <summary>
    /// ��Ϸͼ�������С�仯
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Border_GameIconsArea_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDragRectangles();
    }




    [RelayCommand]
    private void Pin()
    {
        IsPinned = !IsPinned;
        if (IsPinned)
        {
            FontIcon_Pin.Glyph = IconUnpin;
            GameIconsAreaVisible = true;
        }
        else
        {
            FontIcon_Pin.Glyph = IconPin;
            // �����ڹ̶�ʱ������ǰ��Ϸ��ȡ���̶������Ͻǵ�ͼ�겻�ı������
            var temp = CurrentGameBizIcon;
            CurrentGameBizIcon = null;
            CurrentGameBizIcon = temp;
            if (!FullBackgroundVisible)
            {
                GameIconsAreaVisible = false;
            }
        }
        AppSetting.IsGameBizSelectorPinned = IsPinned;
    }




    #endregion






    #region Full Background ��ɫ��͸������



    public bool FullBackgroundVisible => Border_FullBackground.Opacity > 0;


    [RelayCommand]
    private void ShowFullBackground()
    {
        Border_FullBackground.Opacity = 1;
        Border_FullBackground.IsHitTestVisible = true;
        GameIconsAreaVisible = true;
    }


    private void HideFullBackground()
    {
        Border_FullBackground.Opacity = 0;
        Border_FullBackground.IsHitTestVisible = false;
        if (!IsPinned)
        {
            GameIconsAreaVisible = false;
        }
    }


    private void Border_FullBackground_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        var position = e.GetPosition(sender as UIElement);
        if (position.X <= Border_CurrentGameIcon.ActualWidth && position.Y <= Border_CurrentGameIcon.ActualHeight)
        {
            Border_FullBackground.Opacity = 0;
            Border_FullBackground.IsHitTestVisible = false;
        }
        else
        {
            HideFullBackground();
        }
    }



    #endregion






    public void OnLanguageChanged(object? sender, LanguageChangedMessage message)
    {
        if (message.Completed)
        {
            this.Bindings.Update();
        }
    }









}

