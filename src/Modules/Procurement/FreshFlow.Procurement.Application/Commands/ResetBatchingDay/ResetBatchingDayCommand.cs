using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.ResetBatchingDay;

public sealed record ResetBatchingDayCommand(
    DateOnly TargetDate,
    string Confirmation,
    Guid ActorId) : ICommand<BatchingResetResult>;
