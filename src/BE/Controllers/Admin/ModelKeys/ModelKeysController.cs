using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Admin.ModelKeys.Dtos;
using Chats.BE.Controllers.Common;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Common;
using Chats.BE.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Admin.ModelKeys;

[Route("api/admin/model-keys"), AuthorizeAdmin]
public class ModelKeysController(ChatsDB db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ModelKeyDto[]>> GetAllModelKeys(CancellationToken cancellationToken)
    {
        ModelKeyDto[] result = await db.ModelKeys
            .OrderByDescending(x => x.Id)
            .Select(x => new ModelKeyDto
            {
                Id = x.Id,
                ModelProviderId = x.ModelProviderId,
                Name = x.Name,
                Host = x.Host,
                Secret = x.Secret,
                CreatedAt = x.CreatedAt,
                EnabledModelCount = x.Models.Count(x => !x.IsDeleted),
                TotalModelCount = x.Models.Count
            })
            .ToArrayAsync(cancellationToken);

        for (int i = 0; i < result.Length; i++)
        {
            ModelKeyDto modelKey = result[i];
            result[i] = modelKey.WithMaskedKeys();
        }

        return Ok(result);
    }

    [HttpPut("{modelKeyId}")]
    public async Task<ActionResult> UpdateModelKey(short modelKeyId, [FromBody] UpdateModelKeyRequest request, CancellationToken cancellationToken)
    {
        ModelKey? modelKey = await db.ModelKeys
            .FirstOrDefaultAsync(x => x.Id == modelKeyId, cancellationToken);
        if (modelKey == null)
        {
            return NotFound();
        }

        if (!await db.ModelProviders.AnyAsync(x => x.Id == request.ModelProviderId, cancellationToken))
        {
            return this.BadRequestMessage("Invalid model provider");
        }
        modelKey.ModelProviderId = request.ModelProviderId;
        modelKey.Name = request.Name;
        if (!modelKey.Secret.IsMaskedEquals(request.Secret))
        {
            modelKey.Secret = request.Secret;
        }
        modelKey.Host = request.Host;
        if (db.ChangeTracker.HasChanges())
        {
            modelKey.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult> CreateModelKey([FromBody] UpdateModelKeyRequest request, CancellationToken cancellationToken)
    {
        if (!await db.ModelProviders.AnyAsync(x => x.Id == request.ModelProviderId, cancellationToken))
        {
            return this.BadRequestMessage("Invalid model provider");
        }

        ModelKey newModelKey = new()
        {
            ModelProviderId = request.ModelProviderId,
            Name = request.Name,
            Host = request.Host,
            Secret = request.Secret,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ModelKeys.Add(newModelKey);
        await db.SaveChangesAsync(cancellationToken);

        return Created(default(string), value: newModelKey.Id);
    }

    [HttpDelete("{modelKeyId}")]
    public async Task<ActionResult> DeleteModelKey(short modelKeyId, CancellationToken cancellationToken)
    {
        if (await db.Models.AnyAsync(m => m.ModelKeyId == modelKeyId, cancellationToken))
        {
            return this.BadRequestMessage("Model key is in use");
        }

        ModelKey? modelKey = await db.ModelKeys
            .FirstOrDefaultAsync(x => x.Id == modelKeyId, cancellationToken);
        if (modelKey == null)
        {
            return NotFound();
        }

        db.ModelKeys.Remove(modelKey);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("{modelKeyId:int}/possible-models")]
    public async Task<ActionResult<PossibleModelDto[]>> ListModelKeyPossibleModels(short modelKeyId, [FromServices] ChatFactory cf, CancellationToken cancellationToken)
    {
        ModelKey? modelKey = await db
           .ModelKeys
           .Include(x => x.Models)
           .AsSplitQuery()
           .FirstOrDefaultAsync(x => x.Id == modelKeyId, cancellationToken);

        if (modelKey == null)
        {
            return NotFound();
        }

        DBModelProvider modelProvider = (DBModelProvider)modelKey.ModelProviderId;
        ModelLoader? loader = cf.CreateModelLoader(modelProvider);
        if (loader != null)
        {
            string[] models = await loader.ListModels(modelKey, cancellationToken);
            HashSet<string> existsDeploymentNames = await db.Models
                .Where(x => x.ModelKeyId == modelKeyId && x.DeploymentName != null)
                .Select(x => x.DeploymentName!)
                .ToHashSetAsync(cancellationToken);

            if (modelProvider == DBModelProvider.Ollama || modelProvider == DBModelProvider.OpenRouter)
            {
                Dictionary<short, ModelReference> referenceOptions = await db.ModelReferences
                    .Where(x => x.ProviderId == modelKey.ModelProviderId)
                    .ToDictionaryAsync(k => k.Id, v => v, cancellationToken);

                return Ok(models.Select(model =>
                {
                    bool isVision = 
                        model.Contains("qvq", StringComparison.OrdinalIgnoreCase) ||
                        model.Contains("vision", StringComparison.OrdinalIgnoreCase) ||
                        model.Contains("vl", StringComparison.OrdinalIgnoreCase);
                    // 1401, 1400 -> ollama, 1801, 1800 -> openrouter
                    short modelReferenceId = (short)((int)modelProvider * 100 + (isVision ? 1 : 0));
                    return new PossibleModelDto()
                    {
                        DeploymentName = model,
                        ReferenceId = modelReferenceId,
                        ReferenceName = referenceOptions[modelReferenceId].Name,
                        IsLegacy = referenceOptions[modelReferenceId].PublishDate switch
                        {
                            var x when x < new DateOnly(2024, 7, 1) => true,
                            _ => false
                        },
                        IsExists = existsDeploymentNames.Contains(model),
                    };
                }));
            }
            else
            {
                Dictionary<string, ModelReference> referenceOptions = await db.ModelReferences
                    .Where(x => x.ProviderId == modelKey.ModelProviderId)
                    .ToDictionaryAsync(k => k.Name, v => v, cancellationToken);
                HashSet<string> referenceOptionNames = [.. referenceOptions.Keys];

                return Ok(models.Select(model =>
                {
                    string bestMatch = FuzzyMatcher.FindBestMatch(model, referenceOptionNames);

                    return new PossibleModelDto()
                    {
                        DeploymentName = model,
                        ReferenceId = referenceOptions[bestMatch].Id,
                        ReferenceName = referenceOptions[bestMatch].Name,
                        IsLegacy = false,
                        IsExists = existsDeploymentNames.Contains(model),
                    };
                }).ToArray());
            }
        }
        else
        {
            PossibleModelDto[] readyRefs = await db.ModelReferences
                .Where(x => x.ProviderId == modelKey.ModelProviderId)
                .OrderBy(x => x.Name)
                .Select(x => new PossibleModelDto()
                {
                    DeploymentName = x.Models.FirstOrDefault(m => m.ModelKeyId == modelKeyId)!.DeploymentName,
                    ReferenceId = x.Id,
                    ReferenceName = x.Name,
                    IsLegacy = x.PublishDate != null && x.PublishDate < new DateOnly(2024, 7, 1),
                    IsExists = x.Models.Any(m => m.ModelKeyId == modelKeyId),
                })
                .OrderBy(x => (x.IsLegacy ? 1 : 0) + (x.IsExists ? 2 : 0))
                .ThenByDescending(x => x.ReferenceId)
                .ToArrayAsync(cancellationToken);

            return Ok(readyRefs);
        }
    }
}
