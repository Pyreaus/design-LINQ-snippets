               // [...]  controller class definition up here ^       
               
        // GET: api/v1/Offer/GetOffers/{token}
        [ActionName("GetOffers"),HttpGet("[controller]/[action]/{token:int}")]
        public async Task<IActionResult> GetOffers([FromRoute] int token)
        {
            IEnumerable<Offer?> offers = await offerService.GetOffersAsync(token);
            //automapper here - convert to view model
            return offers is not null and IEnumerable<Offer> ? Ok(offers) : StatusCode(204);
        }               
        // PUT: api/v1/Offer/Edit/5
        [ActionName("Edit"),HttpPut("[controller]/[action]/{id:guid}")]
        public async Task<IActionResult<int?>> Edit([FromRoute] Guid id, [FromBody] AddModifyOfferVM addModifyOfferVM)
        {   
            if (await offerService.GetById(id) is null or not Offer _) return StatusCode(204); 
          
            Offer offerToUpdate = offerService.GetById(id);                                                            
            offerToUpdate.Caption = addModifyOfferVM.Caption;           //
            offerToUpdate.Description = addModifyOfferVM.Description;  //
            offerToUpdate.ImgPath = addModifyOfferVM.ImgPath;         //
            offerService.Update(offerToUpdate);                                // Should be using AutoMapper here with Ignore() in map configuration 

            return Ok(200);
        }
        // GET: api/v1/Offer/GetOffer/5
        [ActionName("GetOffer"),HttpGet("[controller]/[action]/{id:guid}")]
        public async Task<IActionResult<int?, OfferViewModel?>> GetOffer([FromRoute] Guid id)
        {
            if (await offerService.GetById(id) is null or not Offer _) return StatusCode(204); 
            Offer offer = offerService.GetById(id);

            PeopleFinderUser peopleFinderUser = userService.GetById(offer.CreatorId);
            OfferViewModel offerVM = new OfferViewModel
            {
                Id = offer.Id,
                ImgPath = offer.ImgPath,
                Caption = offer.Caption,
                Description = offer.Description,
                Timestamped = offer.Timestamped,
                Email = peopleFinderUser.Email,
                Telephone = peopleFinderUser.Telephone,
                FirstName = peopleFinderUser.FirstName,
                LastName = peopleFinderUser.LastName,
                Photo = bnetUrl + peopleFinderUser.Photo
            };
            
            return Ok(offerVM);
        }
        // DELETE: api/v1/Offer/DeleteOffer/5
        [ActionName("DeleteOffer"),HttpDelete("[controller]/[action]/{id:guid}")]
        public async Task<IActionResult<Offer?,int?>> DeleteOffer([FromRoute] Guid id)
        {
            if (await offerService.GetById(id) is null or not Offer _) return StatusCode(204); 
          
            Offer offerToDelete = offerService.GetById(id);
            offerService.Delete(offerToDelete);
            
            return Ok(200);
        }


//TODO: create Upload() controller for file uploads
