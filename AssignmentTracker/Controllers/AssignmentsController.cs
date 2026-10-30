using AssignmentTracker.Models;
using AssignmentTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentTracker.Controllers;

/// <summary>
/// CRUD endpoints for the "assignments" resource, stored in PostgreSQL.
///
/// [ApiController] turns on API-friendly behavior: request bodies are read from JSON
/// automatically, and malformed JSON gets an automatic 400 Bad Request.
/// </summary>
[ApiController]
[Route("api/assignments")]
public class AssignmentsController : ControllerBase
{
    private readonly AssignmentRepository _repository;

    // The repository is registered in Program.cs; ASP.NET passes it in here (dependency injection).
    public AssignmentsController(AssignmentRepository repository)
    {
        _repository = repository;
    }


    // GET /api/assignments  -> 200 with every assignment (an empty array when there are none)
    [HttpGet]
    public async Task<ActionResult<List<Assignment>>> GetAll(CancellationToken ct)
    {
        return Ok(await _repository.GetAllAsync(ct));
    }


    // GET /api/assignments/{id}  -> 200 with one assignment, or 404
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Assignment>> GetById(int id, CancellationToken ct)
    {
        var assignment = await _repository.GetByIdAsync(id, ct);

        if (assignment is null)
        {
            return AssignmentNotFound(id);
        }

        return Ok(assignment);
    }


    // POST /api/assignments  -> 201 Created with the new assignment and a Location header, or 400
    [HttpPost]
    public async Task<ActionResult<Assignment>> Create(AssignmentInput input, CancellationToken ct)
    {
        if (!IsValid(input))
        {
            return ValidationProblem(ModelState);
        }

        var created = await _repository.AddAsync(input, ct);

        // Sets the Location header to /api/assignments/{id} of the new assignment
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }


    // PUT /api/assignments/{id}  -> 200 with the updated assignment, 400, or 404
    [HttpPut("{id:int}")]
    public async Task<ActionResult<Assignment>> Update(int id, AssignmentInput input, CancellationToken ct)
    {
        if (!IsValid(input))
        {
            return ValidationProblem(ModelState);
        }

        var updated = await _repository.UpdateAsync(id, input, ct);

        if (updated is null)
        {
            return AssignmentNotFound(id);
        }

        return Ok(updated);
    }


    // DELETE /api/assignments/{id}  -> 204 No Content, or 404
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!await _repository.DeleteAsync(id, ct))
        {
            return AssignmentNotFound(id);
        }

        return NoContent();
    }


    // Runs validation rules and copies any errors into ModelState,
    // so ValidationProblem(ModelState) returns them as a standard 400 response.
    private bool IsValid(AssignmentInput input)
    {
        foreach (var (field, messages) in input.Validate())
        {
            foreach (var message in messages)
            {
                ModelState.AddModelError(field, message);
            }
        }

        return ModelState.IsValid;
    }


    private ObjectResult AssignmentNotFound(int id)
    {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Assignment not found",
            detail: $"No assignment exists with id {id}.");
    }
}
