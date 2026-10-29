using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Domain;

/// <summary>
/// One reason a write to an index field was refused.
/// </summary>
/// <param name="FieldName">The technical name of the field that was refused.</param>
/// <param name="Reason">A complete, human-readable sentence naming the limit that was broken.</param>
public sealed record FieldViolation(string FieldName, string Reason);
