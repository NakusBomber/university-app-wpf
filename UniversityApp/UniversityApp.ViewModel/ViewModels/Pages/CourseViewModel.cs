﻿using Microsoft.EntityFrameworkCore;
using Ninject;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using UniversityApp.Model.Entities;
using UniversityApp.Model.Interfaces;
using UniversityApp.ViewModel.Commands;
using UniversityApp.ViewModel.Interfaces;
using UniversityApp.ViewModel.Models;
using UniversityApp.ViewModel.ViewModels.Dialogs;

namespace UniversityApp.ViewModel.ViewModels.Pages;

public class CourseViewModel : ViewModelBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWindowService<CourseDialogViewModel, CourseDialogResult> _courseDialogService;
    private readonly IWindowService<MessageBoxViewModel> _messageBoxService;

    private ObservableCollection<Course>? _courses;

    public ObservableCollection<Course> Courses
    {
        get
        {
            if (_courses == null)
            {
                _courses = new ObservableCollection<Course>();
            }
            return _courses;
        }
        set
        {
            _courses = value;
            OnPropertyChanged();
        }
    }

    private Course? _selectedCourse;
    public Course? SelectedCourse
    {
        get => _selectedCourse;
        set
        {
            _selectedCourse = value;
            OnPropertyChanged();
        }
    }

    public IAsyncCommand OpenCreateCourseDialogCommand {  get; set; }
    public IAsyncCommand OpenUpdateCourseDialogCommand {  get; set; }
    public IAsyncCommand DeleteCourseCommand { get; set; }
    public IAsyncCommand ReloadCoursesCommand {  get; set; }

    [Inject]
    public CourseViewModel(
        IUnitOfWork unitOfWork,
        IWindowService<CourseDialogViewModel,CourseDialogResult> courseDialogService,
        IWindowService<MessageBoxViewModel> messageBoxService)
    {
        _unitOfWork = unitOfWork;
        _courseDialogService = courseDialogService;
        _messageBoxService = messageBoxService;

        OpenCreateCourseDialogCommand = AsyncCommand.Create(OpenCreateCourseDialogAsync);
        OpenUpdateCourseDialogCommand = new AsyncCommand<object?>(async _ =>
        {
            await OpenUpdateCourseDialogAsync();
            return null;
        }, CanOpenUpdateCourseDialog);
        DeleteCourseCommand = new AsyncCommand<object?>(async _ =>
        {
            await DeleteCourseAsync();
            return null;
        }, CanDeleteCourse);
        ReloadCoursesCommand = AsyncCommand.Create(GetAllCoursesAsync);
    }

    private async Task OpenCreateCourseDialogAsync(CancellationToken cancellationToken = default)
    {
        var newVM = new CourseDialogViewModel("Create course", CloseActiveWindow);

        CourseDialogResult result = _courseDialogService.Show(newVM);
        if (result.IsSuccess && result.Course != null)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1)).ConfigureAwait(false);
                await _unitOfWork.CourseRepository.CreateAsync(result.Course);
                await _unitOfWork.SaveAsync();
                await GetAllCoursesAsync();
            }
            catch (DbUpdateException)
            {
                await OpenErrorMessageBoxAsync("Already is a course by that name");
            }
            catch (Exception e)
            {
                await OpenErrorMessageBoxAsync(e.Message);
            }
        }
    }

    private async Task OpenUpdateCourseDialogAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedCourse == null)
        {
            throw new ArgumentNullException(nameof(SelectedCourse));
        }

        var newVM = new CourseDialogViewModel("Change course", CloseActiveWindow)
        {
            Name = SelectedCourse!.Name ?? string.Empty,
            Description = SelectedCourse!.Description ?? string.Empty,
        };

        CourseDialogResult result = _courseDialogService.Show(newVM);
        if (result.IsSuccess && result.Course != null)
        {
            try
            {
                var courses = await _unitOfWork.CourseRepository.GetAsync(c => c.Id == SelectedCourse.Id);
                var course = courses.FirstOrDefault();
                if (course == null)
                {
                    throw new ArgumentNullException(nameof(course));
                }
                course.Name = result.Course.Name;
                course.Description = result.Course.Description;

                if (!course.FullCompare(SelectedCourse))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1)).ConfigureAwait(false);
                    await _unitOfWork.CourseRepository.UpdateAsync(course);
                    await _unitOfWork.SaveAsync();
                    await GetAllCoursesAsync();
                    SelectedCourse = null;
                }
            }
            catch (DbUpdateConcurrencyException e)
            {
                await OpenErrorMessageBoxAsync(e.Message);
            }
            catch (DbUpdateException)
            {
                await OpenErrorMessageBoxAsync("Already is a course by that name");
            }
            catch (Exception e)
            {
                await OpenErrorMessageBoxAsync(e.Message);
            }
        }
    }

    private bool CanOpenUpdateCourseDialog(object? parameter) => SelectedCourse != null;
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
        Application.Current.Windows.OfType<Window>().First(w => w.IsActive == true)?.Close();
    }

    private async Task DeleteCourseAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedCourse == null)
        {
            throw new ArgumentNullException(nameof(SelectedCourse));
        }
        await _unitOfWork.CourseRepository.DeleteAsync(SelectedCourse);
        await _unitOfWork.SaveAsync();
        SelectedCourse = null;
        await GetAllCoursesAsync();
    }

    private bool CanDeleteCourse(object? parameter)
    {
        return SelectedCourse != null && SelectedCourse.Groups.Count == 0;
    }

    private async Task GetAllCoursesAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(1)).ConfigureAwait(false);
        var list = await _unitOfWork.CourseRepository.GetAsync(asNoTracking: true);
        Courses = new ObservableCollection<Course>(list);
    }
}
