[ApiController]  //[..] imports & namespace ^                 
[Authorize(Policy="tracr-default",AuthenticationSchemes=NegotiateDefaults.AuthenticationScheme)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[Produces(MediaTypeNames.Application.Json)]
[Route("api/v1/[controller]")]
public sealed partial class {type}Controller : ControllerBase     //i.e. {type} = User /  UserController
{
    #region [Infrastructure]
    private readonly IMapper _mapper;
    private readonly ServerOptions servers;
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserController> _logger;
    private readonly ClaimsPrincipal _claimsPrincipal;
    private static TE Ex<TE>(object? exc=null) where TE : Exception => throw (TE)Activator.CreateInstance(typeof(TE), "untracked", nameof(exc))!;
    private static TE Ex<TE, T>(object? exc) where TE : Exception => throw (TE)Activator.CreateInstance(typeof(TE), $"Expected: {typeof(T)}", nameof(exc))!;

    public UserController(ClaimsPrincipal claimsPrincipal, IOptions<ServerOptions> options, ILogger<UserController> logger, IConfiguration configuration, IMapper mapper, IUserService userService)
    {
        (servers, _userService, _configuration, _logger, _mapper, _claimsPrincipal) = (options.Value, userService ?? throw Ex<ArgumentNullException>(), configuration, logger, mapper, claimsPrincipal);
    }
    #endregion

    /// <summary>
    /// GET: {{host}}/api/{{version}}/Diary/GetSkills
    /// </summary>
    /// <response code="200"><see cref="IEnumerable{Skill}"/> objects</response>
    /// <response code="204"><see cref="IEnumerable{Skill}"/> objects not found</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status200OK,Type=typeof(IEnumerable<Skill>))]
    [ActionName("GetSkills"),Authorize(Policy="trainee//reviewer"),HttpGet("[action]")]
    public async Task<ActionResult<IEnumerable<Skill?>?>> GetSkills(CancellationToken ctk = default)
    {
        IEnumerable<Skill?> skills = await _diaryService.GetSkills(ctk);
        return (skills != null) && (typeof(List<Skill>) == skills!.GetType()) ? Ok(skills) : StatusCode(204);
    }
    
    /// <summary>
    /// GET: {{host}}/api/{{version}}/User/GetTraineesByReviewer/[pfid]
    /// </summary>
    /// <param name="pfid">trainee reviwer PFID</param>
    /// <response code="500">internal error</response>
    /// <response code="404"><see cref="IEnumerable{TraineeViewModel}"/> objects not found</response>
    /// <response code="200"><see cref="IEnumerable{TraineeViewModel}"/> objects</response>
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status200OK,Type=typeof(IEnumerable<TraineeViewModel>))]
    [ActionName("GetTraineesByReviewer"),Authorize(Policy="reviewer"),HttpGet("[action]/{pfid:int}")]
    public async Task<ActionResult<IEnumerable<TraineeViewModel?>?>> GetTraineesByReviewer(CancellationToken ctk = default, [FromRoute] [ValidPfid] int pfid)
    {
        IEnumerable<PeopleFinderUser?> users = await _userService.GetPFUsersAsync(ctk);
        IEnumerable<Trainee?> trainees = await _userService.TraineesByReviewerAsync(pfid, ctk);
        if ((users is null)||(trainees is null)) return StatusCode(404);
        IEnumerable<TraineeViewModel?> traineesVM = _mapper.Map<IEnumerable<Trainee?>,IEnumerable<TraineeViewModel>>(
            trainees.Where(
                trainee => users.Any(user => user?.PFID.ToString() == trainee?.TRAINEE_PFID)
            ).OfType<Trainee>().ToList()!).OfType<TraineeViewModel>().ToList();
        foreach (PeopleFinderUser? user in users) user!.Photo = (bnetUrl + user.Photo?.ToString()) ?? "../../../assets/profilePic.png";
        foreach (TraineeViewModel? trainee in traineesVM) _mapper.Map(users.FirstOrDefault(user => trainee?.TRAINEE_PFID == user?.PFID.ToString())!, trainee);
        return (trainees.GetType() == typeof(List<Trainee>)) && traineesVM != null ? Ok(traineesVM) : StatusCode(500);
    }

    /// <summary>
    /// GET: {{host}}/api/{{version}}/User/GetReviewers
    /// </summary>
    /// <response code="200"><see cref="IEnumerable{UserViewModel}"/> objects</response>
    /// <response code="204"><see cref="IEnumerable{UserViewModel}"/> objects not found</response>
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK,Type=typeof(IEnumerable<UserViewModel>))]
    [ActionName("GetReviewers"),Authorize(Policy="admin"),HttpGet("[action]")]
    public async Task<ActionResult<IEnumerable<UserViewModel?>?>> GetReviewers(CancellationToken ctk = default)
    {
        IEnumerable<PeopleFinderUser?> reviewers = await _userService.GetReviewersAsync(ctk);
        IEnumerable<UserViewModel?> reviewersVM = _mapper.Map<IEnumerable<PeopleFinderUser?>,IEnumerable<UserViewModel>>(reviewers!);
        foreach(UserViewModel? rev in reviewersVM) 
        {
            rev!.Role = "reviewer";
            rev!.Photo = (bnetUrl + rev!.Photo?.ToString()) ?? "../../../assets/profilePic.png";
        }
        return (reviewersVM != null) && (typeof(List<PeopleFinderUser>) == reviewers!.GetType()) ? Ok(reviewersVM) : StatusCode(204);
    }

    /// <summary>
    /// PUT: {{host}}/api/{{version}}/User/SetPair/[pfid]
    /// </summary>
    /// <param name="pfid">PFID of trainee</param>
    /// <param name="addReq">request DTO</param>
    /// <response code="500">internal error</response>
    /// <response code="400"><see cref="Trainee"/> not modified</response>
    /// <response code="201"><see cref="Trainee"/> modified</response>
    [ValidateAntiForgeryToken]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status201Created,Type=typeof(TraineeViewModel))]
    [ActionName("SetPair"),Authorize(Policy="admin"),HttpPut("[action]/{pfid:int}")]
    public async Task<ActionResult<TraineeViewModel>?> SetPair(
    [FromRoute] [ValidPfid] int pfid, [FromBody] AddModifyTraineeReq addReq, CancellationToken ctk = default)
    {
        Trainee? currentTrainee = await _userService.GetTraineeByPfidAsync(pfid, ctk);
        if ((currentTrainee is null)||(addReq is null)||(await _userService.GetPFUserAsync(pfid, ctk) is null)) return StatusCode(204);
        _userService.SetPair(_mapper.Map(addReq, currentTrainee!), ctk);
        TraineeViewModel traineeVM = _mapper.Map<Trainee,TraineeViewModel>(currentTrainee!);
        return traineeVM != null ? CreatedAtAction(nameof(GetTraineesByReviewer), new { pfid = currentTrainee?.REVIEWER_PFID }, traineeVM) : StatusCode(500);
    }
    
    /// <summary>
    /// GET: {{host}}/api/{{version}}/User/GetUserType
    /// </summary>
    /// <response code="500">internal error</response>
    /// <response code="511">unauthorized client</response>
    /// <response code="200"><see cref="UserViewModel"/> object</response>
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status511NetworkAuthenticationRequired)]
    [ProducesResponseType(StatusCodes.Status200OK,Type=typeof(UserViewModel))]
    [ActionName("GetUserType"),HttpGet("[action]")]
    public async Task<ActionResult<UserViewModel?>> GetUserType(CancellationToken ctk = default, [FromServices] IWebHostEnvironment webHostEnvironment)
    {
        IWebHostEnvironment env = webHostEnvironment ?? NullArg<IWebHostEnvironment>(webHostEnvironment!);
        if (_claimsPrincipal.Identity?.IsAuthenticated == true)
        {
            Claim? usernameClaim = _claimsPrincipal.FindFirst("DomainUsername");
            if (usernameClaim?.Value != null)
            {
                PeopleFinderUser? user = await _userService.GetByDomainAsync(usernameClaim.Value, ctk);
                if (user != null && user?.PFID != null)
                {
                    string? role = await _userService.GetRoleByPfidAsync((int)user.PFID, ctk);
                    UserViewModel? userVM = role != null ? _mapper.Map<PeopleFinderUser,UserViewModel>(user) : null;
                    userVM!.Photo = (bnetUrl + user.Photo?.ToString()) ?? "../../../assets/profilePic.png";
                    userVM!.Role = role ?? "Unauthorized";
                    return userVM != null ? Ok(userVM) : StatusCode(StatusCodes.Status511NetworkAuthenticationRequired);
                }
                return StatusCode(StatusCodes.Status511NetworkAuthenticationRequired);
            }
         }
        return env.IsDevelopment() ? throw new Exception() : StatusCode(StatusCodes.Status500InternalServerError);
    }
    
    /// <summary>
    /// GET: {{host}}/api/{{version}}/Diary/GetDiariesPfid/[pfid]
    /// </summary>
    /// <param name="pfid">PFID of diary objects</param>
    /// <response code="200"><see cref="IEnumerable{DiaryViewModel}"/> objects</response>
    /// <response code="204"><see cref="IEnumerable{DiaryViewModel}"/> objects not found</response>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status200OK,Type=typeof(IEnumerable<DiaryViewModel>))]
    [ActionName("GetDiariesPfid"),Authorize(Policy="trainee//reviewer"),HttpGet("[action]/{pfid:int}")]
    public async Task<ActionResult<IEnumerable<DiaryViewModel?>?>> GetDiariesPfid(CancellationToken ctk = default, [FromRoute] [ValidPfid] int pfid)
    {
        IEnumerable<Diary?> diaries = await _diaryService.GetDiariesAsync(pfid, ctk);
        IEnumerable<DiaryViewModel?> diaryVM = _mapper.Map<IEnumerable<Diary?>, IEnumerable<DiaryViewModel>>(diaries!);
        return (diaryVM != null) && (typeof(List<Diary>) == diaries!.GetType()) ? Ok(diaryVM) : StatusCode(204);
    }

    /// <summary>
    /// GET: {{host}}/api/{{version}}/User/GetUserReviewer
    /// </summary>
    /// <param name="pfid">PFID of trainee</param>
    /// <response code="500">internal error</response>
    /// <response code="200"><see cref="UserViewModel"/> object</response>
    /// <response code="400"><see cref="UserViewModel"/> object not found</response>
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status200OK,Type=typeof(UserViewModel))]
    [ActionName("GetUserReviewer"),Authorize(Policy="trainee//reviewer"),HttpGet("[action]/{pfid:int}")]
    public async Task<ActionResult<UserViewModel?>> GetUserReviewer(CancellationToken ctk = default, [FromRoute] [ValidPfid] int pfid)
    {
        Trainee? trainee = await _userService.GetTraineeByPfidAsync(pfid, ctk);
        PeopleFinderUser? reviewer = await _userService.ReviewerByTraineeAsync(pfid, ctk);
        if ((trainee is null)||(reviewer is null)) return StatusCode(400);
        UserViewModel userVM = _mapper.Map<PeopleFinderUser?,UserViewModel>(reviewer);
        userVM!.Role = "Reviewer";
        userVM!.Photo = (bnetUrl + userVM.Photo?.ToString()) ?? "../../../assets/profilePic.png";    
        return userVM != null ? Ok(userVM) : StatusCode(500);
    }


    /// <summary>
    /// POST: {{host}}/api/{{version}}/User/AssignTrainees/[pfid]
    /// </summary>
    /// <param name="addReq">request DTO</param>
    /// <param name="pfid">PFID of trainee</param>
    /// <response code="500">internal error</response>
    /// <response code="201"><see cref="TraineeViewModel"/> object</response>
    /// <response code="400"><see cref="TraineeViewModel"/> object not created</response>
    [ValidateAntiForgeryToken]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status201Created,Type=typeof(TraineeViewModel))]
    [ActionName("AssignTrainees"),Authorize(Policy="admin"),HttpPost("[action]/{pfid:int}")]
    public async Task<ActionResult<TraineeViewModel?>> AssignTrainees(CancellationToken ctk = default, [FromRoute] [ValidPfid] int pfid, [FromBody] AddModifyTraineeReq addReq)
    {
        if ((await _userService.GetPFUserAsync(pfid, ctk) is null)||(addReq is null)) return StatusCode(400);
        Trainee? newTrainee = _mapper.Map<AddModifyTraineeReq,Trainee>(addReq!);
        newTrainee.TRAINEE_PFID = pfid.ToString();
        _userService.AssignTrainees(newTrainee, ctk);   
        TraineeViewModel traineeVM = _mapper.Map<Trainee,TraineeViewModel>(newTrainee!);
        return traineeVM != null ? CreatedAtAction(nameof(GetTraineesByReviewer), new { pfid = newTrainee?.REVIEWER_PFID }, traineeVM) : StatusCode(500);
    }

    /// <summary>
    /// PUT: {{host}}/api/{{version}}/User/EditTrainee/[pfid]
    /// </summary>
    /// <param name="pfid">PFID of trainee</param>
    /// <param name="modifyReq">request DTO</param>
    /// <response code="200"><see cref="AddModifyTraineeReq"/> object modified</response>
    /// <response code="400"><see cref="Trainee"/> object not modified</response>
    [ValidateAntiForgeryToken]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status200OK,Type=typeof(TraineeViewModel))]
    [ActionName("EditTrainee"),Authorize(Policy="admin//reviewer"),HttpPut("[action]/{pfid:int}")]
    public async Task<ActionResult<TraineeViewModel?>> EditTrainee(CancellationToken ctk = default, [FromRoute] [ValidPfid] int pfid, [FromBody] AddModifyTraineeReq modifyReq)
    {
        Trainee? trainee = await _userService.GetTraineeByPfidAsync(pfid, ctk);
        if ((trainee is null)||(modifyReq is null)) return StatusCode(400);
        _mapper.Map<AddModifyTraineeReq?,Trainee>(modifyReq, trainee);
        this._userService.UpdateTrainee(trainee, ctk);
        return StatusCode(200);
    }


    /// <summary>
    /// GET: {{host}}/api/{{version}}/Employee/GetEmployee/[id]
    /// </summary>
    /// <param name="id">Guid of employee</param>
    /// <response code="200">{employee view object}</response>
    /// <response code="204">invlaid id</response>
    [Obsolete("Maintenance")]
    //--------------------------
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status200OK,Type=typeof(EmployeeViewModel))]
    [ActionName("GetEmployee"),Authorize(Policy="admin"),HttpGet("[action]/{id:guid}")]
    public async Task<ActionResult<EmployeeViewModel?>> GetEmployee(CancellationToken ctk = default, [FromRoute] Guid id)
    {
        Employee? employee = await _employeeService.GetEmployeeByIdAsync(id, ctk);
        EmployeeViewModel employeeVM = _mapper.Map<Employee, EmployeeViewModel>(employee!);
        return (employee != null) && (typeof(Employee) == employee.GetType()) ? Ok(employeeVM) : StatusCode(204);
    }
    

    //[..]
}
