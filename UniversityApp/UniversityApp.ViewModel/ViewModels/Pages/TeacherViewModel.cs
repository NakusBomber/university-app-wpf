﻿using Microsoft.EntityFrameworkCore;
using Ninject;
using System.Collections.ObjectModel;
using System.Windows;
using UniversityApp.Model.Entities;
using UniversityApp.Model.Interfaces;
using UniversityApp.ViewModel.Commands;
using UniversityApp.ViewModel.Interfaces;
using UniversityApp.ViewModel.Models;
using UniversityApp.ViewModel.ViewModels.Dialogs;

namespace UniversityApp.ViewModel.ViewModels.Pages;

public class TeacherViewModel : ViewModelBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWindowService<TeacherDialogViewModel, TeacherDialogResult> _teacherDialogService;
    private readonly IWindowService<MessageBoxViewModel> _messageBoxService;

    private ObservableCollection<Teacher> _teachers = new();

    public ObservableCollection<Teacher> Teachers
    {
        get => _teachers;
        set
        {
            _teachers = value;
            OnPropertyChanged();
        }
    }

    private Teacher? _selectedTeacher;

    public Teacher? SelectedTeacher
    {
        get => _selectedTeacher;
        set
        {
            _selectedTeacher = value;
            OnPropertyChanged();
        }
    }

    public IAsyncCommand<object?> LoadTeachersCommand { get; }
    public IAsyncCommand<object?> OpenCreateTeacherCommand { get; }
    public IAsyncCommand<object?> OpenUpdateTeacherCommand { get; }
    public IAsyncCommand<object?> DeleteTeacherCommand { get; }

    [Inject]
    public TeacherViewModel(
        IUnitOfWork unitOfWork,
        IWindowService<TeacherDialogViewModel, TeacherDialogResult> teacherDialogService,
        IWindowService<MessageBoxViewModel> messageBoxService)
    {
        _unitOfWork = unitOfWork;
        _teacherDialogService = teacherDialogService;
        _messageBoxService = messageBoxService;

        LoadTeachersCommand = AsyncCommand.Create(ReloadAllTeachersAsync);
        DeleteTeacherCommand = AsyncCommand.Create(DeleteTeacherAsync, CanDeleteTeacher);
        OpenCreateTeacherCommand = AsyncCommand.Create(OpenCreateTeacherDialogAsync);
        OpenUpdateTeacherCommand = AsyncCommand.Create(OpenUpdateTeacherAsync, IsTeacherSelected);
    }

    private async Task OpenUpdateTeacherAsync(CancellationToken cancellationToken = default)
    {
        if(SelectedTeacher == null)
        {
            throw new ArgumentNullException(nameof(SelectedTeacher));
        }

        var newVM = new TeacherDialogViewModel("Change teacher", CloseActiveWindow)
        {
            FirstName = SelectedTeacher.FirstName ?? string.Empty,
            LastName = SelectedTeacher.LastName ?? string.Empty,
        };

        var result = _teacherDialogService.Show(newVM);

        if(result.IsSuccess && result.Teacher != null)
        {
            await HandleDbExceptions(async () =>
            {
                var teacher = await _unitOfWork.TeacherRepository.GetByIdAsync(SelectedTeacher.Id);
                teacher.FirstName = result.Teacher.FirstName;
                teacher.LastName = result.Teacher.LastName;

                if (!Entity.AreEntitiesEqual(teacher, SelectedTeacher))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1)).ConfigureAwait(false);
                    await _unitOfWork.TeacherRepository.UpdateAsync(teacher);
                    SelectedTeacher = null;
                    await SaveAndReloadAsync();
                }
            });
        }
    }

    private async Task OpenCreateTeacherDialogAsync(CancellationToken cancellationToken = default)
    {
        var newVM = new TeacherDialogViewModel("Create teacher", CloseActiveWindow);
        var result = _teacherDialogService.Show(newVM);

        if (result.IsSuccess && result.Teacher != null)
        {
            await HandleDbExceptions(async () =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1)).ConfigureAwait(false);
                await _unitOfWork.TeacherRepository.CreateAsync(result.Teacher);
                await SaveAndReloadAsync();
            });
        }
    }

    private async Task DeleteTeacherAsync(CancellationToken cancellationToken = default)
    {
        if(SelectedTeacher == null)
        {
            throw new ArgumentNullException(nameof(SelectedTeacher));
        }

        var teacher = await _unitOfWork.TeacherRepository.GetByIdAsync(SelectedTeacher.Id);
        await _unitOfWork.TeacherRepository.DeleteAsync(teacher);
        SelectedTeacher = null;
        await SaveAndReloadAsync();
    }

    private bool CanDeleteTeacher(object? arg)
    {
        return SelectedTeacher != null && SelectedTeacher.Groups.Count == 0;
    }

    private async Task ReloadAllTeachersAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(1)).ConfigureAwait(false);
        var list = await _unitOfWork.TeacherRepository.GetAsync(asNoTracking: true);
        Teachers = new ObservableCollection<Teacher>(list);
    }

    private async Task OpenErrorMessageBoxAsync(string message, CancellationToken cancellationToken = default)
    {
        var messageViewModel = new MessageBoxViewModel(
            "Error",
            message,
            CloseActiveWindow
        );

        await _messageBoxService.ShowAsync(messageViewModel);
    }

    private void CloseActiveWindow()
    {
        Application.Current.Windows.OfType<Window>().First(w => w.IsActive)?.Close();
    }

    private async Task HandleDbExceptions(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception e)
        {
            await OpenErrorMessageBoxAsync(e.Message);
        }
    }
    private async Task SaveAndReloadAsync(CancellationToken cancellationToken = default)
    {
        await _unitOfWork.SaveAsync();
        await ReloadAllTeachersAsync();
    }
    private bool IsTeacherSelected(object? parameter) => SelectedTeacher != null;
}
